﻿using System.Collections.Generic;
using System.Linq;

namespace Hearthstone_Deck_Tracker.Hearthstone.RelatedCardsSystem.Cards.Shaman;

public class Shudderwock: ICardWithRelatedCards
{
	public string GetCardId() => HearthDb.CardIds.Collectible.Shaman.Shudderwock;

	public bool ShouldShowForOpponent(Player opponent)
	{
		var card = Database.GetCardFromId(GetCardId());
		return CardUtils.MayCardBeRelevant(card, Core.Game.CurrentFormat, opponent.OriginalClass) && GetRelatedCards(opponent).Count > 3;
	}

	public List<Card?> GetRelatedCards(Player player) =>
		player.CardsPlayedThisMatch
			.Select(entity => CardUtils.GetProcessedCardFromEntity(entity, player))
			.Where(card => card is { Mechanics: not null } && card.Mechanics.Contains("Battlecry"))
			.ToList();
}
