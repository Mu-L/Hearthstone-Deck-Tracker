﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay.Data;
using Hearthstone_Deck_Tracker.Live.Data;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.Twitch;
using HSReplay.OAuth;
using HSReplay.OAuth.Data;
using HSReplay.Responses;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	// ReSharper disable InconsistentNaming
	internal sealed class HSReplayNetOAuth
	{
		private static readonly JsonSerializer<OAuthData> Serializer;
		private static readonly Lazy<OAuthClient> Client;
		private static readonly Lazy<OAuthData> Data;
		private static readonly Dictionary<string, CacheObj> Cache = new Dictionary<string, CacheObj>();

		private static readonly int[] Ports = { 17781, 17782, 17783, 17784, 17785, 17786, 17787, 17788, 17789 };
		private const string HSReplayNetClientId = "jIpNwuUWLFI6S3oeQkO3xlW6UCnfogw1IpAbFXqq";
		private const string TwitchExtensionId = "apwln3g3ia45kk690tzabfp525h9e1";
		private const string SuccessUrl = "https://hsdecktracker.net/hsreplaynet/oauth_success/";
		private const string ErrorUrl = "https://hsdecktracker.net/hsreplaynet/oauth_error/";

		public static event Action? Authenticated;
		public static event Action? LoggedOut;
		public static event Action? TwitchUsersUpdated;
		public static event Action? AccountDataUpdated;
		public static event Action? CollectionUpdated;
		public static event Action? UploadTokenClaimed;

		private class CacheObj
		{
			private readonly DateTime _created;

			public CacheObj(object data)
			{
				_created = DateTime.Now;
				Data = data;
			}

			public bool Valid => (DateTime.Now - _created).TotalSeconds < 5;
			public object Data { get; }
		}

		static HSReplayNetOAuth()
		{
			Serializer = new JsonSerializer<OAuthData>("hsreplay_oauth", true);
			Data = new Lazy<OAuthData>(Serializer.Load);
			Client = new Lazy<OAuthClient>(LoadClient);
		}

		private static readonly Scope[] _requiredScopes = { Scope.FullAccess };

		private static OAuthClient LoadClient()
		{
			return new OAuthClient(HSReplayNetClientId, Helper.GetUserAgent(), Data.Value.TokenData);
		}

		public static void Save() => Serializer.Save(Data.Value);

		public static async Task<bool> Authenticate(string? successUrl = null, string? errorUrl = null)
		{
			Log.Info("Authenticating with HSReplay.net...");
			string url;
			try
			{
				url = Client.Value.GetAuthenticationUrl(_requiredScopes, Ports);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
			if(string.IsNullOrEmpty(url))
			{
				Log.Error("Authentication failed, could not create callback listener");
				return false;
			}
			var callbackTask = Client.Value.ReceiveAuthenticationCallback(successUrl ?? SuccessUrl,
				errorUrl ?? ErrorUrl);
			if(!Helper.TryOpenUrl(url))
			{
				ErrorManager.AddError("Could not open your browser.",
					"Please open the following url in your browser to continue:\n\n" + url, true);
			}
			Log.Info("Waiting for callback...");
			var data = await callbackTask;
			if(data == null)
			{
				Log.Error("Authentication failed, received no data");
				return false;
			}
			Data.Value.Code = data.Code;
			Data.Value.RedirectUrl = data.RedirectUrl;
			Data.Value.TokenData = null;
			Log.Info("Authentication complete");
			await UpdateToken();
			Save();
			Log.Info("Claiming upload token if necessary");
			if(!Account.Instance.TokenClaimed.HasValue)
				await ApiWrapper.UpdateUploadTokenStatus();
			if(Account.Instance.TokenClaimed == false)
			{
				if(Account.Instance.UploadToken == null)
				{
					Log.Error("Authentication failed, no upload token found");
					return false;
				}
				await ClaimUploadToken(Account.Instance.UploadToken);
			}
			Authenticated?.Invoke();
			return true;
		}

		public static async Task Logout()
		{
			Serializer.DeleteCacheFile();
			Data.Value.Account = null;
			Data.Value.Code = null;
			Data.Value.RedirectUrl = null;
			Data.Value.TokenData = null;
			Data.Value.TokenDataCreatedAt = DateTime.MinValue;
			Data.Value.TwitchUsers = null;
			Save();
			Account.Instance.Reset();
			await ApiWrapper.UpdateUploadTokenStatus();
			LoggedOut?.Invoke();
		}

		public static async Task<bool> UpdateToken()
		{
			var data = Data.Value;
			if(data.TokenData != null && data.TokenDataCreatedAt != null && (DateTime.Now - data.TokenDataCreatedAt.Value).TotalSeconds < data.TokenData.ExpiresIn)
				return true;
			if(string.IsNullOrEmpty(data.Code) || string.IsNullOrEmpty(data.RedirectUrl))
			{
				Log.Error("Could not update token, we don't have a code or redirect url.");
				return false;
			}
			if(!string.IsNullOrEmpty(data.TokenData?.RefreshToken))
			{
				Log.Info("Refreshing token data...");
				try
				{
					var tokenData = await Client.Value.RefreshToken();
					if(tokenData != null)
					{
						SaveTokenData(tokenData);
						return true;
					}
				}
				catch(Exception e)
				{
					Log.Error(e);
				}
			}
			try
			{
				Log.Info("Fetching new token...");
				var tokenData = await Client.Value.GetToken(data.Code, data.RedirectUrl);
				if(tokenData == null)
				{
					Log.Error("We did not receive any token data.");
					return false;
				}
				SaveTokenData(tokenData);
				return true;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task<bool> UpdateTwitchUsers()
		{
			Log.Info("Fetching twitch accounts...");
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var twitchAccounts = await Client.Value.GetTwitchAccounts();
				Data.Value.TwitchUsers = twitchAccounts;
				Save();
				Log.Info($"Saved {twitchAccounts.Count} account(s): {string.Join(", ", twitchAccounts.Select(x => x.Username))}");
				TwitchUsersUpdated?.Invoke();
				return twitchAccounts.Count != 0;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task<bool> UpdateAccountData()
		{
			Log.Info("Updating account data...");
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var account = await Client.Value.GetHSReplayNetAccount();
				Data.Value.Account = account;
				Save();
				Log.Info($"Found account: {account?.Username ?? "None"}");
				AccountDataUpdated?.Invoke();
				return account != null;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static bool IsFullyAuthenticated => IsAuthenticatedFor(_requiredScopes);

		public static bool IsAuthenticatedFor(params Scope[] scopes)
		{
			if(string.IsNullOrEmpty(Data.Value.TokenData?.Scope))
				return false;
			var currentScopes = Data.Value.TokenData!.Scope.Split(' ');
			if(currentScopes.Contains(Scope.FullAccess.Name))
				return true;
			return scopes.All(s => currentScopes.Contains(s.Name));
		}

		public static bool IsAuthenticatedForAnything()
			=> !string.IsNullOrEmpty(Data.Value.TokenData?.Scope);

		public static List<TwitchAccount>? TwitchUsers => Data.Value.TwitchUsers;

		public static User? AccountData => Data.Value.Account;

		public static void SaveTokenData(TokenData data)
		{
			Data.Value.TokenData = data;
			Data.Value.TokenDataCreatedAt = DateTime.Now;
			Save();
			Log.Info("Saved token data");
		}

		public static async Task SendTwitchPayload(Payload payload)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return;
				}
				var response = await Client.Value.SendTwitchUpdate(Config.Instance.SelectedTwitchUser, TwitchExtensionId, payload);
				Log.Debug(response);
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
		}

		public static async Task<UserCurrentVideo?> GetCurrentVideo(int user_id)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				CurrentVideo video = await Client.Value.GetUserCurrentVideo(user_id, TwitchExtensionId);
				if(Cache.TryGetValue(video.Url, out var cache) && cache.Valid)
					return (UserCurrentVideo) cache.Data;
				var data = TwitchApiHelper.GenerateTwitchVodUrl(video.Url, video.CreatedAt, video.Date);
				var userCurrentVideo = new UserCurrentVideo(data, video.Language);
				Cache[video.Url] = new CacheObj(userCurrentVideo);
				return userCurrentVideo;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		public static async Task<bool> IsStreaming(int user_id)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				return await Client.Value.IsUserStreaming(user_id, TwitchExtensionId);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task<bool> UpdateCollection(Collection collection)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var response = await Client.Value.UploadCollection(collection, collection.AccountHi, collection.AccountLo, CollectionType.Constructed);
				Log.Debug(response);
				CollectionUpdated?.Invoke();
				return true;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task<bool> UpdateMercenariesCollection(MercenariesCollection collection)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var response = await Client.Value.UploadCollection(collection, collection.AccountHi, collection.AccountLo, CollectionType.Mercenaries);
				Log.Debug(response);
				return true;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		internal static async Task<bool> ClaimUploadToken(string token)
		{
			UploadTokenHistory.Write("Trying to claim " + token);
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var response = await Client.Value.ClaimUploadToken(token);
				UploadTokenHistory.Write($"Claimed {token}: {response}");
				Log.Debug(response);
				UploadTokenClaimed?.Invoke();
				return true;
			}
			catch(Exception e)
			{
				UploadTokenHistory.Write($"Error claming {token}\n" + e);
				Log.Error(e);
				return false;
			}
		}

		internal static async Task<ClaimBlizzardAccountResponse> ClaimBlizzardAccount(ulong accountHi, ulong accountLo,
			string battleTag)
		{
			var account = $"hi={accountHi}, lo={accountLo}, battleTag={battleTag}";
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return ClaimBlizzardAccountResponse.Error;
				}

				var response = await Client.Value.ClaimBlizzardAccount(accountHi, accountLo, battleTag);
				Log.Debug($"Claimed {account}: {response}");
				return ClaimBlizzardAccountResponse.Success;
			}
			catch(WebException e)
			{
				Log.Error(e);
				try
				{
					using(var stream = e.Response.GetResponseStream())
					using(var reader = new StreamReader(stream))
					{
						var response = JsonConvert.DeserializeObject<dynamic>(reader.ReadToEnd());
						if(response.error == "account_already_claimed")
							return ClaimBlizzardAccountResponse.TokenAlreadyClaimed;
					}
				}
				catch
				{
				}
				return ClaimBlizzardAccountResponse.Error;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return ClaimBlizzardAccountResponse.Error;
			}
		}

		internal enum ClaimBlizzardAccountResponse
		{
			Success,
			Error,
			TokenAlreadyClaimed
		}
		public static async Task<string?> IdentifyClientAnalyticsToken(string token = "")
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				return await Client.Value.IdentifyClientAnalyticsToken(token);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		public static async Task<T?> MakeRequest<T>(Func<OAuthClient, Task<T>> request) where T : class
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				var response = await request(Client.Value);
				Log.Debug(response.ToString());
				return response;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		public static async Task<HttpResponseMessage?> SendAsyncWithAuth(HttpRequestMessage req)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				req.Headers.Accept.ParseAdd("application/json");
				var authToken = Data.Value.TokenData!;
				req.Headers.Authorization = new AuthenticationHeaderValue(authToken.TokenType, authToken.AccessToken);
				req.Headers.UserAgent.ParseAdd(Helper.GetUserAgent());
				return await Core.HttpClient.SendAsync(req);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}
	}
}
