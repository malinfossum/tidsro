namespace Tidsro.Models;

/// <summary>What an import should restore. Cancel is the answer in every ambiguous case — Esc, the
/// title-bar X and Enter all land here.</summary>
public enum ImportChoice { Cancel, AlarmsOnly, Everything }
