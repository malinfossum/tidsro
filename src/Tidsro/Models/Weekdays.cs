namespace Tidsro.Models;

/// <summary>The set of weekdays a recurring alarm repeats on. Serialises as one integer.</summary>
[Flags]
public enum Weekdays
{
    None = 0,
    Mon = 1,
    Tue = 2,
    Wed = 4,
    Thu = 8,
    Fri = 16,
    Sat = 32,
    Sun = 64,
}
