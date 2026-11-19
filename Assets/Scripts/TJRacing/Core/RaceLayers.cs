// TJ Racing 3D — physics layer indices for the shield power-up.
//
// Unity's built-in layers are 0-7, so 8 and 9 are free user layers.
// Layer INDICES work at runtime whether or not they are named in
// ProjectSettings/TagManager.asset (names are editor display-only),
// so no TagManager edit is needed — these constants are the single
// source of truth for the indices.
//
// Shield (see RaceManager.ApplyPowerup): while active, the player
// car (layer PlayerCar) ignores collisions with traffic cars
// (layer Traffic) and passes through them. AI cars stay on Default
// and still collide with traffic, exactly as before.
namespace TJRacing
{
    public static class RaceLayers
    {
        public const int Traffic = 8;    // traffic cars (TrafficManager.SpawnOne)
        public const int PlayerCar = 9;  // player race car root (CarFactory.Build, isPlayer)
    }
}
