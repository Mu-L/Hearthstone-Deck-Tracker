﻿namespace Hearthstone_Deck_Tracker.Hearthstone.RelatedCardsSystem.Cards.Hunter;

public class StranglethornHeart: ResurrectionCard
{
	public override string GetCardId() => HearthDb.CardIds.Collectible.Hunter.StranglethornHeart;

	protected override bool FilterCard(Card card) => card.IsBeast() && card.Cost >= 5;

	protected override bool ResurrectsMultipleCards() => true;

	public override bool ShouldShowForOpponent(Player opponent)
	{
		var card = Database.GetCardFromId(GetCardId());
		return CardUtils.MayCardBeRelevant(card, Core.Game.CurrentFormat, opponent.OriginalClass) && GetRelatedCards(opponent).Count > 1;
	}
}
