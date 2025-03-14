﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Commands;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;
using static System.Windows.Visibility;
using static Hearthstone_Deck_Tracker.HsReplay.Enums.AccountStatus;

namespace Hearthstone_Deck_Tracker.FlyoutControls.Options.HSReplay
{
	public partial class HSReplayAccount : INotifyPropertyChanged
	{
		private bool _logoutButtonEnabled = true;
		private bool _logoutTriggered;
		private bool _claimTokenButtonEnabled = true;

		public HSReplayAccount()
		{
			InitializeComponent();
			HSReplayNetOAuth.AccountDataUpdated += () =>
			{
				Update();
				LogoutTriggered = false;
			};
			HSReplayNetOAuth.LoggedOut += () =>
			{
				Update();
				LogoutTriggered = false;
			};
			HSReplayNetOAuth.UploadTokenClaimed += () => OnPropertyChanged(nameof(UploadTokenUnclaimed));
			Account.Instance.TokenClaimedChanged += () => OnPropertyChanged(nameof(UploadTokenUnclaimed));
			ConfigWrapper.ReplayAutoUploadChanged += () => OnPropertyChanged(nameof(ReplayUploadingEnabled));
			ConfigWrapper.CollectionSyncingChanged += () => OnPropertyChanged(nameof(CollectionSyncingEnabled));
		}

		public bool IsAuthenticated => HSReplayNetOAuth.IsFullyAuthenticated;

		public Visibility ReplaysClaimedVisibility =>
			Account.Instance.Status == Anonymous || HSReplayNetOAuth.IsFullyAuthenticated ? Collapsed : Visible;

		public Visibility LoginInfoVisibility =>
			Account.Instance.Status == Anonymous && !HSReplayNetOAuth.IsFullyAuthenticated ? Visible : Collapsed;

		public bool HasSubscription => Subscriptions != SubscriptionStatus.None;

		public string SubscriptionStatusText => Subscriptions switch
		{
			SubscriptionStatus.Premium => LocUtil.Get("Options_HSReplay_Account_Subscription_Premium"),
			SubscriptionStatus.Tier7 => LocUtil.Get("Options_HSReplay_Account_Subscription_Tier7"),
			SubscriptionStatus.Bundle => LocUtil.Get("Options_HSReplay_Account_Subscription_Bundle"),
			_ => LocUtil.Get("Options_HSReplay_Account_Subscription_Generic"),
		};

		public SubscriptionStatus Subscriptions
		{
			get
			{
				var isPremium = HSReplayNetOAuth.AccountData?.IsPremium == true;
				var isTier7 = HSReplayNetOAuth.AccountData?.IsTier7 == true;

				if(isPremium && isTier7)
					return SubscriptionStatus.Bundle;

				if(isPremium)
					return SubscriptionStatus.Premium;

				if(isTier7)
					return SubscriptionStatus.Tier7;

				return SubscriptionStatus.None;
			}
		}

		public Visibility LogoutWarningVisibility => LogoutTriggered ? Visible : Collapsed;

		public bool LogoutTriggered
		{
			get => _logoutTriggered;
			set
			{
				_logoutTriggered = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(LogoutWarningVisibility));
			}
		}

		public bool LogoutButtonEnabled
		{
			get => _logoutButtonEnabled;
			set
			{
				_logoutButtonEnabled = value;
				OnPropertyChanged();
			}
		}

		public string Username => HSReplayNetOAuth.AccountData?.Username ?? Account.Instance.Username ?? string.Empty;

		public ICommand LogoutCommand => new Command(async () =>
		{
			if(LogoutTriggered)
			{
				LogoutButtonEnabled = false;
				await HSReplayNetOAuth.Logout();
				LogoutButtonEnabled = true;
			}
			else
				LogoutTriggered = true;
		});

		public bool ClaimTokenButtonEnabled
		{
			get => _claimTokenButtonEnabled;
			set
			{
				_claimTokenButtonEnabled = value;
				OnPropertyChanged();
			}
		}

		public ICommand EnableCollectionSyncingCommand => new Command(() => ConfigWrapper.CollectionSyncingEnabled = true);

		public ICommand EnableReplayUploadingCommand => new Command(() => ConfigWrapper.HsReplayAutoUpload = true);

		public ICommand PremiumInfoCommand => new Command(() =>
		{
			var url = Helper.BuildHsReplayNetUrl("premium", "options_account_premium_info");
			Helper.TryOpenUrl(url);
		});

		public ICommand AccountSettingsCommand => new Command(() =>
		{
			var url = Helper.BuildHsReplayNetUrl("account", "options_account_settings");
			Helper.TryOpenUrl(url);
		});

		public ICommand ClaimUploadTokenCommand => new Command(async () =>
		{
			ClaimTokenButtonEnabled = false;
			if(!Account.Instance.TokenClaimed.HasValue)
				await ApiWrapper.UpdateUploadTokenStatus();
			if(Account.Instance.TokenClaimed == false && Account.Instance.UploadToken != null)
				await HSReplayNetOAuth.ClaimUploadToken(Account.Instance.UploadToken);
			ClaimTokenButtonEnabled = true;
		});

		public bool ReplayUploadingEnabled => ConfigWrapper.HsReplayAutoUpload;

		public bool CollectionSyncingEnabled => ConfigWrapper.CollectionSyncingEnabled;

		public bool UploadTokenUnclaimed => IsAuthenticated && (!Account.Instance.TokenClaimed ?? true);

		public void Update()
		{
			OnPropertyChanged(nameof(Username));
			OnPropertyChanged(nameof(IsAuthenticated));
			OnPropertyChanged(nameof(ReplaysClaimedVisibility));
			OnPropertyChanged(nameof(LoginInfoVisibility));
			OnPropertyChanged(nameof(Subscriptions));
			OnPropertyChanged(nameof(HasSubscription));
			OnPropertyChanged(nameof(SubscriptionStatusText));
			OnPropertyChanged(nameof(UploadTokenUnclaimed));
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public enum SubscriptionStatus
		{
			None,
			Premium,
			Tier7,
			Bundle
		}
	}
}
