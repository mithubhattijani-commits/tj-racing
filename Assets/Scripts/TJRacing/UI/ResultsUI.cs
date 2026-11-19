// TJ Racing 3D — results screen.
// Shows position, race time, best lap and the Level Coins from the result.
// The Rs value is FLUTTER's job (research §A.7 — Flutter overlays it from the
// server-confirmed balance). Continue is the ONLY place an interstitial may
// appear (research §A.6: never right after a win — only after Continue is tapped).
using UnityEngine;

namespace TJRacing
{
    public class ResultsUI : MonoBehaviour
    {
        private Canvas _canvas;
        private UnityEngine.UI.Text _titleText;
        private UnityEngine.UI.Text _detailText;
        private UnityEngine.UI.Text _coinsText;
        private bool _interstitialOffered;

        void Awake() { BuildUI(); }

        void Start()
        {
            if (RaceManager.Instance != null)
                RaceManager.Instance.OnRaceFinished += ShowResults;
            Hide();
        }

        private void BuildUI()
        {
            _canvas = UIBuilder.MakeCanvas(gameObject, "ResultsCanvas");
            var bg = UIBuilder.MakePanel(_canvas.transform, "Bg",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.82f));
            _titleText = UIBuilder.Label(bg, "Title", "", 110, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_titleText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), new Vector2(-500, -80), new Vector2(500, 80));
            _detailText = UIBuilder.Label(bg, "Detail", "", 44, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(_detailText.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(-500, -70), new Vector2(500, 70));
            _coinsText = UIBuilder.Label(bg, "Coins", "", 52, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_coinsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500, -60), new Vector2(500, 60));
            var againBtn = UIBuilder.MakeButton(bg, "AgainBtn", "RACE AGAIN", 48, UIBuilder.Green, OnRaceAgain);
            UIBuilder.Place(againBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(-350, -55), new Vector2(350, 55));
            var continueBtn = UIBuilder.MakeButton(bg, "ContinueBtn", "CONTINUE", 48, UIBuilder.Cyan, OnContinue);
            UIBuilder.Place(continueBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(-350, -55), new Vector2(350, 55));
        }

        private void ShowResults(RaceResult result)
        {
            _interstitialOffered = false;
            string title = result.position == 1 ? "YOU WIN!"
                         : result.position == 2 ? "2ND PLACE!"
                         : "FINISHED " + result.position + OrdinalSuffix(result.position);
            _titleText.text = title;
            _titleText.color = result.position == 1 ? UIBuilder.Gold : Color.white;
            _detailText.text = "Time " + FormatTime(result.race_time_secs)
                             + "  •  Best lap " + FormatTime(result.best_lap_secs);
            _coinsText.text = result.level_coins_earned > 0
                ? "+" + result.level_coins_earned + " Level Coins"
                : "No coins this time — win or finish 2nd to earn coins";
            if (result.position == 1) AudioManager.Instance?.PlayWin();
            UnityAdsManager.Instance?.NotifyRaceFinished(result.position == 1);
            _canvas.gameObject.SetActive(true);
        }

        private void OnRaceAgain()
        {
            Hide();
            TJRacingBootstrap.Instance?.RestartRace();
        }

        private void OnContinue()
        {
            // Interstitial ONLY here — after Continue, never on the win itself.
            if (!_interstitialOffered)
            {
                _interstitialOffered = true;
                UnityAdsManager.Instance?.MaybeShowInterstitialAfterContinue();
            }
            Hide();
            TJRacingBootstrap.Instance?.LoadMenuScene();
        }

        public void Hide() { _canvas.gameObject.SetActive(false); }

        private static string FormatTime(float secs)
        {
            int m = Mathf.FloorToInt(secs / 60f);
            int s = Mathf.FloorToInt(secs % 60f);
            int ms = Mathf.FloorToInt((secs * 100f) % 100f);
            return m.ToString("00") + ":" + s.ToString("00") + "." + ms.ToString("00");
        }

        private static string OrdinalSuffix(int n)
        {
            return n == 1 ? "ST" : n == 2 ? "ND" : n == 3 ? "RD" : "TH";
        }
    }
}
