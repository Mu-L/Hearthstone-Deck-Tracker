using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using BobsBuddy.Anomalies;
using BobsBuddy.Simulation;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Utility.Analytics
{
	internal class Influx
	{
		private static DateTime _appStartTime;
		private static bool _new;
		private static int? _pctHsReplayData;
		private static int? _pctHsReplayDataTotal;
		private static readonly List<int> MainWindowActivations = new List<int>();
		private static DateTime? _lastMainWindowActivation;
		private static DateTime _oAuthInitiated;

		public static void OnAppStart(Version version, bool isNew, bool authenticated, bool premium, int startupDuration, int numPlugins, bool cleanShutdown, bool skippedSplashscreen)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			_appStartTime = DateTime.Now;
			_new = isNew;
			var point = new InfluxPointBuilder("hdt_app_start")
				.Tag("version", version.ToVersionString(true))
				.Tag("new", isNew)
				.Tag("authenticated", authenticated)
				.Tag("premium", premium)
				.Tag("collection_syncing", Config.Instance.SyncCollection)
				.Tag("collections_uploaded", Account.Instance.CollectionState.Count)
				.Tag("mercs_collections_uploaded", Account.Instance.MercenariesCollectionState.Count)
				.Tag("auto_upload", Config.Instance.HsReplayAutoUpload)
				.Tag("lang_card", Helper.GetCardLanguage())
				.Tag("lang_ui", Config.Instance.Localization.ToString())
				.Tag("clean_shutdown", cleanShutdown)
				.Tag("skipped_splashscreen", skippedSplashscreen)
				.Field("num_plugins", numPlugins)
				.Field("startup_duration", startupDuration);
#if(SQUIRREL)
			point.Tag("squirrel", true);
#else
			point.Tag("squirrel", false);
#endif
			WritePoint(point.Build());
		}

		public static void OnAppExit(Version version, bool statsWindowUsed)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var sessionDuration = (DateTime.Now - _appStartTime).TotalSeconds;
			var point = new InfluxPointBuilder("hdt_app_exit")
				.Tag("version", version.ToVersionString(true))
				.Tag("new", _new)
				.Tag("stats_window_used", statsWindowUsed)
				.Field("session_duration_seconds", (int)sessionDuration);
#if(SQUIRREL)
			point.Tag("squirrel", true);
#else
			point.Tag("squirrel", false);
#endif

			if(_pctHsReplayDataTotal.HasValue)
				point.Field("pct_hsreplay_data_total", _pctHsReplayDataTotal.Value);
			if(_pctHsReplayData.HasValue)
				point.Field("pct_hsreplay_data_last14d", _pctHsReplayData.Value);

			if(_lastMainWindowActivation != null)
				OnMainWindowDeactivated();
			point.Field("window_activations", MainWindowActivations.Count);
			point.Field("window_active_duration", (int)MainWindowActivations.Average());

			WritePoint(point.Build());
		}

		public static void OnHearthMirrorExit(int exitCode, string mode)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_hearthmirror_exit").Tag("mode", mode).Build());
		}

		public static void OnHsReplayAutoUploadChanged(bool newState)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_hsreplay_autoupload_changed").Tag("new_state", newState).Build());
		}

		public static void OnHighMemoryUsage(long mem)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_memory_usage", false).Tag("os", Regex.Escape(Helper.GetWindowsVersion()))
				.Tag("net", Helper.GetInstalledDotNetVersion()).Field("MB", mem).Build());
		}

		public static void OnUnevenPermissions()
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_uneven_permissions", false).Tag("os", Regex.Escape(Helper.GetWindowsVersion()))
				.Tag("net", Helper.GetInstalledDotNetVersion()).Build());
		}

		public static void OnPluginLoaded(IPlugin plugin, TimeSpan startupTime)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_plugin_loaded", false)
				.Tag("name", plugin.Name)
				.Tag("version", plugin.Version.ToVersionString())
				.Field("startup_time", (int)startupTime.TotalMilliseconds);
			WritePoint(point.Build());
		}

		public static void OnPluginLoadingError(IPlugin plugin)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_plugin_loading_error", false)
				.Tag("name", plugin.Name)
				.Tag("version", plugin.Version.ToVersionString());
			WritePoint(point.Build());
		}

		public static void OnGameUploadFailed(WebExceptionStatus status = WebExceptionStatus.UnknownError)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_hsreplay_upload_failed_counter").Tag("status", status).Build());
		}

		public static void OnEndOfGameUploadError(string reason)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_end_of_game_upload_error").Tag("reason", Regex.Escape(reason)).Build());
		}

		public static void OnCollectionSyncingBannerClicked(bool authenticated, bool collectionSynced)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_banner_interaction")
				.Tag("type", "click")
				.Tag("authenticated", authenticated)
				.Tag("collection_synced", collectionSynced);
			WritePoint(point.Build());
		}

		public static void OnCollectionSyncingBannerClosed()
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_banner_interaction")
				.Tag("type", "close");
			WritePoint(point.Build());
		}

		public static void OnBlizzardAccountClaimed(bool success)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_account_claimed")
				.Tag("success", success);
			WritePoint(point.Build());
		}

		public static void OnCollectionSynced(bool success)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_uploaded")
				.Tag("success", success);
			WritePoint(point.Build());
		}

		public static void OnMercenariesCollectionSynced(bool success)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_mercenaries_uploaded")
				.Tag("success", success);
			WritePoint(point.Build());
		}

		public static void OnCollectionSyncingEnabled(bool enabled)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_collection_syncing_enabled_changed")
				.Tag("enabled", enabled);
			WritePoint(point.Build());
		}

		public static void OnOAuthLoginInitiated()
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			_oAuthInitiated = DateTime.Now;
		}

		public static void OnOAuthLoginComplete(HSReplayNetHelper.AuthenticationErrorType error)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_oauth_login")
				.Tag("error", error)
				.Field("duration_ms", (int)(DateTime.Now - _oAuthInitiated).TotalMilliseconds);
			WritePoint(point.Build());

		}

		public static void OnOAuthLogout()
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_oauth_logout");
			WritePoint(point.Build());
		}

		public static void OnHsReplayDataLoaded()
		{
			try
			{
				var constructedDecks = DeckList.Instance.Decks.Where(x => !x.IsArenaDeck && !x.IsDungeonDeck && !x.IsDuelsDeck).ToList();
				if(constructedDecks.Count == 0)
					return;

				var available = HsReplayDataManager.Decks.AvailableDecks;
				bool HasData(Deck deck) => available.Contains(deck.ShortId);
				int PctHasData(IReadOnlyCollection<Deck> decks) => (int)Math.Round(100.0 * decks.Count(HasData) / decks.Count);

				_pctHsReplayDataTotal = PctHasData(constructedDecks);

				var decksPlayed = constructedDecks.Where(x => DateTime.Now - x.LastPlayed < TimeSpan.FromDays(14)).ToList();
				if(decksPlayed.Count > 0)
					_pctHsReplayData = PctHasData(decksPlayed);
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
		}

		public static void OnMainWindowActivated()
		{
			_lastMainWindowActivation = DateTime.Now;
		}

		public static void OnMainWindowDeactivated()
		{
			if(_lastMainWindowActivation == null)
				return;
			var duration = DateTime.Now - _lastMainWindowActivation.Value;
			MainWindowActivations.Add((int)duration.TotalSeconds);
			_lastMainWindowActivation = null;
		}

		public static void OnBobsBuddySimulationCompleted(
			CombatResult result, Output output, int turn, Anomaly? anomaly, bool terminalCase,
			bool isDuos, bool isOpposingAkazamzarak
		)
		{
#if(SQUIRREL)
			if(!Config.Instance.GoogleAnalytics)
				return;
			var point = new InfluxPointBuilder("hdt_bb_combat_result_v3")
				.HighPrecision()
				.Tag("result", result.ToString())
				.Tag("terminal_case", terminalCase.ToString())
				.Tag("turn", turn)
				.Tag("bb_version", BobsBuddyUtils.VersionString)
				.Tag("is_duos", isDuos.ToString())
				.Field("iterations", output.simulationCount)
				.Field("result_win", result == CombatResult.Win ? 1 : 0)
				.Field("result_tie", result == CombatResult.Tie ? 1 : 0)
				.Field("result_loss", result == CombatResult.Loss ? 1 : 0)
				.Field("win_rate", output.winRate * 100)
				.Field("tie_rate", output.tieRate * 100)
				.Field("loss_rate", output.lossRate * 100);

			if(anomaly != null)
				point.Tag("anomaly", anomaly.CardID);

			point.Tag("opposing_akazamzarak", isOpposingAkazamzarak.ToString());

			_queue.Add(point.Build());
#endif
		}

		public static void OnBobsBuddyEnabledChanged(bool newState)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_bb_enabled_changed").Tag("new_state", newState).Build());
		}

		public static void OnSessionRecapEnabledChanged(bool newState)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_session_recap_enabled_changed").Tag("new_state", newState).Build());
		}

		public static void OnMulliganToastClose(bool wasClicked, bool hasData)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_mulligan_toast_closed")
				.Tag("was_clicked", wasClicked)
				.Tag("has_data", hasData)
				.Build());
		}

		public static void OnMulliganToastEnabledChanged(bool newState)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_mulligan_toast_enabled_changed").Tag("new_state", newState).Build());
		}

		public static void OnMemoryReading(string methodName, int successCount, int failureCount)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_hearthmirror_read_report", false)
				.Field("success_count", successCount)
				.Field("failure_count", failureCount)
				.Tag("method", methodName).Build()
			);
		}

		public static void OnGetBattlegroundsCompositionStatsError(string reason, string message)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_bgs_composition_stats_error")
				.Tag("reason", Regex.Escape(reason))
				.Field("message", Regex.Escape(message))
				.Build()
			);
		}

		public static void OnBattlegroundsHDTToolsExit(BattlegroundsChinaModule.HDTToolsExitCode exitCode)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_bgs_hdttools_exit_code")
				.Tag("exit_code", exitCode)
				.Build()
			);
		}

		public static void OnBattlegroundsHDTToolsExecutionProblem(BattlegroundsChinaModule.HDTToolsExecutionProblem executionProblem)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_bgs_hdttools_execution_problem")
				.Tag("execution_problem", executionProblem)
				.Build()
			);
		}

		public static void OnGetBattlegroundsCompositionGuidesError(string reason, string message)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_bgs_composition_guides_error")
				.Tag("reason", Regex.Escape(reason))
				.Field("message", Regex.Escape(message))
				.Build()
			);
		}

		public static void OnMulliganGuideDeckSerializationError(string reason, string message)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			WritePoint(new InfluxPointBuilder("hdt_mulligan_guide_params_serialization_error")
				.Tag("reason", Regex.Escape(reason))
				.Field("message", Regex.Escape(message))
				.Build()
			);
		}

		public static void OnSquirrelFindBestRemote(string result)
		{
			if(!Config.Instance.GoogleAnalytics)
				return;
			var timeZone = Regex.Escape(TimeZoneInfo.Local.Id);

			WritePoint(new InfluxPointBuilder("hdt_squirrel_find_best_remote")
				.Tag("result", result)
				.Tag("timezone", timeZone)
				.Build()
			);
		}

		private static readonly List<InfluxPoint> _queue = new();
		public static void SendQueuedMetrics()
		{
			if(!_queue.Any())
				return;
			var points = _queue.ToList();
			_queue.Clear();
			foreach(var group in points.GroupBy(x => x.HighPrecision))
				WritePoints(group, highPrecision: group.Key);
		}

		private static void WritePoint(InfluxPoint point) => WritePoints(new[] { point }, point.HighPrecision);

		private static async void WritePoints(IEnumerable<InfluxPoint> points, bool highPrecision)
		{
			if(!points.Any())
				return;
			try
			{
				using(var client = new UdpClient())
				{
					var line = string.Join("\n", points.Select(x => x.ToLineProtocol()));
					var data = Encoding.UTF8.GetBytes(line);
					var length = await client.SendAsync(data, data.Length, "metrics.hearthsim.net", highPrecision ? 8099 : 8091);
					Log.Debug(line + " - " +  length);
				}
			}
			catch(Exception ex)
			{
				Log.Debug(ex.ToString());
			}
		}
	}
}
