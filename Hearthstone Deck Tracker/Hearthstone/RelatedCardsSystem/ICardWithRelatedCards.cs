﻿using System.Collections.Generic;

namespace Hearthstone_Deck_Tracker.Hearthstone.RelatedCardsSystem;

public interface ICardWithRelatedCards : ICard
{
	bool ShouldShowForOpponent(Player opponent);
	List<Card?> GetRelatedCards(Player player);

}
