using System.Collections.Generic;
using UnityEngine;

// A filing cabinet's drawers or the cork board. The puzzle puts notes here, each its own sheet;
// a spot with no notes can't be used, so only the real hiding places respond to E.
public class ClueSpot : Interactable
{
    public string prompt;
    public readonly List<PlayerInteractor.Note> notes = new List<PlayerInteractor.Note>();

    public override string Prompt => prompt;
    public override bool CanUse => notes.Count > 0;

    public override void Use(PlayerInteractor player) => player.ShowNotes(notes.ToArray());
}
