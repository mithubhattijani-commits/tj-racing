// TJ Racing 3D — runtime configuration.
// Mirrors plan.md §9 ("games.tj_racing" block of tj-earn.json). NOTHING gameplay-critical
// is hardcoded: Flutter passes this JSON into Unity at launch via FlutterBridge.SetConfig().
// Defaults below match the plan's proposed defaults. Unity NEVER credits coins — it only
// reports race results; Flutter + the server (claim-race-level) decide all crediting.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    [Serializable]
    public class LevelCoinsConfig
    {
        public int win = 10;
        public int second = 5;
        public int third = 0;
        public int finish = 0;
        public int daily_cap = 200;
        public int coins_per_pkr = 100;
        public int min_withdrawal_coins = 10000;
        public bool show_rs_value = true;
    }

    [Serializable]
    public class AdCoinsConfig
    {
        public int per_rewarded_ad = 10;
        public int daily_cap = 100;
        public bool cash_convertible = false;
    }

    [Serializable]
    public class AdsConfig
    {
        public bool banner_enabled = true;
        public bool interstitial_enabled = true;
        public int interstitial_every_n_races = 3;
        public int interstitial_min_interval_secs = 180;
        public bool rewarded_enabled = true;
    }

    [Serializable]
    public class TrackDef
    {
        public string id = "night_city_1";
        public string name = "Night City";
        public bool locked = false;
    }

    [Serializable]
    public class RaceConfig
    {
        public int laps = 3;
        public int ai_opponents = 5;
        public bool nitro_enabled = true;
        public List<TrackDef> tracks = new List<TrackDef> { new TrackDef() };
    }

    [Serializable]
    public class CarDef
    {
        public string id;
        public string name;
        public string @class;          // standard | sports | luxury
        public int ad_coins_price;
        // Physics tuning (defaults per car, see CarCatalogue for the shipped table).
        public float topSpeedKmh = 180f;
        public float acceleration = 12f; // m/s^2 at full throttle
        public float handling = 1.0f;    // steering multiplier
        public float nitroCapacity = 100f;
        public string bodyColorHex = "#E5484D";
    }

    [Serializable]
    public class PowerUpDef
    {
        public string id;
        public string name;
        public int ad_coins_price;
    }

    [Serializable]
    public class RacingConfig
    {
        public bool enabled = true;
        public string title = "TJ Racing 3D";
        public LevelCoinsConfig level_coins = new LevelCoinsConfig();
        public AdCoinsConfig ad_coins = new AdCoinsConfig();
        public AdsConfig ads = new AdsConfig();
        public RaceConfig race = new RaceConfig();
        public List<CarDef> cars = new List<CarDef>();
        public List<PowerUpDef> powerups = new List<PowerUpDef>();

        // Competitor-research §A fixes (NOT in plan §9 — applied here as runtime config).
        public float tiltDeadZoneDeg = 10f;      // ±10 deg dead zone (Traffic Racer lesson)
        public float tiltSensitivity = 1.0f;     // 0.5 .. 2.0, slider in Settings
        public int hardLevelThreshold = 3;       // level >= 3: lap-3 road traffic appears
        public int adsMinGapSecs = 90;           // never two ads back-to-back
        public int firstRaceAiSlowdownPct = 25;  // race 1 must be winnable: AI slower

        public static RacingConfig Defaults()
        {
            var c = new RacingConfig();
            c.cars = CarCatalogue.DefaultCars();
            c.powerups = new List<PowerUpDef>
            {
                new PowerUpDef { id = "nitro_boost", name = "Nitro Boost", ad_coins_price = 10 },
                new PowerUpDef { id = "shield", name = "Shield", ad_coins_price = 15 },
            };
            return c;
        }

        public static RacingConfig FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return Defaults();
            try
            {
                var parsed = JsonUtility.FromJson<RacingConfig>(json);
                if (parsed == null) return Defaults();
                if (parsed.cars == null || parsed.cars.Count == 0) parsed.cars = CarCatalogue.DefaultCars();
                if (parsed.level_coins == null) parsed.level_coins = new LevelCoinsConfig();
                if (parsed.ad_coins == null) parsed.ad_coins = new AdCoinsConfig();
                if (parsed.ads == null) parsed.ads = new AdsConfig();
                if (parsed.race == null) parsed.race = new RaceConfig();
                return parsed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TJ Racing] Bad config JSON, using defaults: " + e.Message);
                return Defaults();
            }
        }

        public CarDef GetCar(string id)
        {
            foreach (var car in cars) if (car.id == id) return car;
            return cars.Count > 0 ? cars[0] : null;
        }
    }

    /// <summary>Shipped car table — plan §9 prices are FINAL (research §A.5: keep 200/500/800/1200).</summary>
    public static class CarCatalogue
    {
        public static List<CarDef> DefaultCars()
        {
            return new List<CarDef>
            {
                new CarDef { id = "street_hawk",    name = "Street Hawk",    @class = "standard", ad_coins_price = 0,    topSpeedKmh = 178f, acceleration = 11f, handling = 1.00f, nitroCapacity = 100f, bodyColorHex = "#E5484D" },
                new CarDef { id = "night_falcon",   name = "Night Falcon",   @class = "sports",   ad_coins_price = 200,  topSpeedKmh = 192f, acceleration = 12.5f, handling = 1.05f, nitroCapacity = 100f, bodyColorHex = "#2E6BFF" },
                new CarDef { id = "midnight_ghost", name = "Midnight Ghost", @class = "sports",   ad_coins_price = 500,  topSpeedKmh = 205f, acceleration = 14f, handling = 1.10f, nitroCapacity = 110f, bodyColorHex = "#7A4DFF" },
                new CarDef { id = "storm_rider",    name = "Storm Rider",    @class = "luxury",   ad_coins_price = 800,  topSpeedKmh = 218f, acceleration = 15.5f, handling = 1.15f, nitroCapacity = 120f, bodyColorHex = "#FFB020" },
                new CarDef { id = "thunder_king",   name = "Thunder King",   @class = "luxury",   ad_coins_price = 1200, topSpeedKmh = 232f, acceleration = 17f, handling = 1.20f, nitroCapacity = 130f, bodyColorHex = "#12C2A0" },
            };
        }
    }
}
