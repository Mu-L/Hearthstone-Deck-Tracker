using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media.Imaging;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Importing.Game.ImportOptions;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Importing.Game
{
	public class ImportedDeck
	{
		/// <param name="deck">HearthMirror deck object</param>
		/// <param name="matches">Local decks with HsId OR 30 matching cards</param>
		/// <param name="localDecks">All local decks</param>
		public ImportedDeck(HearthMirror.Objects.Deck deck, List<Deck>? matches, IList<Deck> localDecks)
		{
			Deck = deck;
			matches ??= new List<Deck>();
			var hero = new Card(deck.Hero);
			if(!hero.IsKnownCard || string.IsNullOrEmpty(hero.PlayerClass))
			{
				Log.Error("Unknown hero with CardID=" + deck.Hero);
				return;
			}
			var lowerClass = hero.PlayerClass!.ToLower();
			Class = lowerClass == "demonhunter" ? "DemonHunter" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lowerClass);

			var localOptions = localDecks.Where(d => d.Class == Class && !d.Archived && !d.IsArenaDeck)
				.Select(x => new ExistingDeck(x, deck));
			var matchesOptions = matches.Select(x => new ExistingDeck(x, deck)).ToList();
			var importOptions = matchesOptions.Concat(localOptions)
				.GroupBy(x => new {x.Deck.DeckId, x.Deck.Version}).Select(g => g.First())
				.OrderByDescending(x => x.Deck.HsId == deck.Id)
				.ThenBy(x => x.MismatchingCards)
				.ThenByDescending(x => x.Deck.LastPlayed);

			ImportOptions = New.Concat(importOptions);

			SelectedIndex = matchesOptions.Any(x => !x.ShouldBeNewDeck) ? 1 : 0;
		}

		public HearthMirror.Objects.Deck Deck { get; }
		public string? Class { get; }
		public bool Import { get; set; } = true;
		public IEnumerable<IImportOption>? ImportOptions { get; }
		public int SelectedIndex { get; set; }

		public IImportOption SelectedImportOption => ImportOptions.ElementAt(SelectedIndex);
		public BitmapImage ClassImage => ImageCache.GetClassIcon(Class);
		private IEnumerable<IImportOption> New => new IImportOption[] { new NewDeck() };
	}
}
