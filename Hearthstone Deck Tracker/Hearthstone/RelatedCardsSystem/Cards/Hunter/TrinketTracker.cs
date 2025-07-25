using System.Collections.Generic;

namespace Hearthstone_Deck_Tracker.Hearthstone.RelatedCardsSystem.Cards.Hunter;

public class TrinketTracker : ICardWithHighlight
{
	public string GetCardId() => HearthDb.CardIds.Collectible.Hunter.TrinketTracker;

	public HighlightColor ShouldHighlight(Card card, IEnumerable<Card> deck) =>
		HighlightColorHelper.GetHighlightColor(card is { Type: "Spell", Cost: 1 });
}
