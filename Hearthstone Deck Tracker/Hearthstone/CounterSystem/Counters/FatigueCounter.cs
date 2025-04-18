﻿using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.LogReader.Interfaces;
using Hearthstone_Deck_Tracker.Utility;
using Entity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;

namespace Hearthstone_Deck_Tracker.Hearthstone.CounterSystem.Counters;

public class FatigueCounter : NumericCounter
{
	public override string LocalizedName => LocUtil.Get("Counter_Fatigue", useCardLanguage: true);

	protected override string? CardIdToShowInUI => HearthDb.CardIds.NonCollectible.Neutral.SkullOfGuldanSTORMWIND1;
	public override string[] RelatedCards => new string[]
	{
		HearthDb.CardIds.Collectible.Warlock.BaritoneImp,
		HearthDb.CardIds.Collectible.Warlock.Crescendo,
		HearthDb.CardIds.Collectible.Warlock.EncroachingInsanity,
		HearthDb.CardIds.Collectible.Warlock.CrazedConductor,
		HearthDb.CardIds.NonCollectible.Warlock.CurseofAgony_AgonyToken
	};

	/**
	 * If these cards are in the *friendly* deck they should show the *opponent's* counter.
	 */
	public string[] RelatedCardsForOpponent => new string[]
	{
		HearthDb.CardIds.Collectible.Warlock.EncroachingInsanity,
		HearthDb.CardIds.Collectible.Warlock.CurseOfAgony,
	};

	public FatigueCounter(bool controlledByPlayer, GameV2 game) : base(controlledByPlayer, game)
	{
	}

	public override bool ShouldShow()
	{
		if(!Game.IsTraditionalHearthstoneMatch) return false;
		if(IsPlayerCounter)
			return Counter > 0 || InPlayerDeckOrKnown(RelatedCards);
		return Counter > 0 || InPlayerDeckOrKnown(RelatedCardsForOpponent);
	}

	public override string[] GetCardsToDisplay()
	{
		return IsPlayerCounter ?
			GetCardsInDeckOrKnown(RelatedCards).ToArray() :
			GetCardsInDeckOrKnown(RelatedCardsForOpponent).Concat(
				FilterCardsByClassAndFormat(RelatedCards, Game.Opponent.OriginalClass)
			).ToArray();
	}

	public override string ValueToShow() => (Counter + 1).ToString();

	public override void HandleTagChange(GameTag tag, IHsGameState gameState, Entity entity, int value, int prevValue)
	{
		if(!Game.IsTraditionalHearthstoneMatch)
			return;

		if(tag != GameTag.FATIGUE)
			return;

		if(value == 0)
			return;

		var controller = entity.GetTag(GameTag.CONTROLLER);

		if(controller == Game.Player.Id && IsPlayerCounter || controller == Game.Opponent.Id && !IsPlayerCounter)
			Counter = value;
	}
}
