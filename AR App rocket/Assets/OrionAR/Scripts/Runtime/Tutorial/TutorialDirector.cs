using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Runs the guided tour of the spacecraft.
    ///
    /// Rebuilt after the previous version miscounted. Two design errors caused that,
    /// and both are fixed here by construction rather than by patching:
    ///
    ///   The old version only credited the one component the tour was currently
    ///   pointing at. Tapping anything else showed its description but advanced
    ///   nothing, so the counter sat still while the user felt they were making
    ///   progress. Now every component counts the moment it is opened, in whatever
    ///   order the user finds them, and the highlighted one is only a suggestion.
    ///
    ///   The count was recomputed in several places that could disagree. Now a single
    ///   set of studied ids is the only source of truth, every change goes through
    ///   MarkStudied, and one event tells the UI to redraw. There is nowhere for a
    ///   second opinion to live.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [Header("Tour")]
        [Tooltip("Leave empty to collect every SelectablePart in the scene, ordered by PartInfo.order.")]
        public List<SelectablePart> parts = new List<SelectablePart>();

        [Tooltip("Seconds a component's panel must be open before it counts as studied.")]
        public float dwellToCredit = 0.75f;

        [Tooltip("Pulse the suggested next component so there is always somewhere to go.")]
        public bool hintNextPart = true;

        [Header("Audio")]
        public AudioSource narrationSource;

        // --- the single source of truth -------------------------------
        readonly HashSet<string> _studied = new HashSet<string>();

        /// <summary>Raised whenever progress changes. (studied, total)</summary>
        public event System.Action<int, int> ProgressChanged;

        /// <summary>Raised when a specific part's studied state flips.</summary>
        public event System.Action<SelectablePart> PartStudied;

        /// <summary>Raised once every component has been studied.</summary>
        public event System.Action TourCompleted;

        public int StudiedCount { get { return _studied.Count; } }
        public int TotalParts { get { return parts.Count; } }
        public bool IsComplete { get { return parts.Count > 0 && _studied.Count >= parts.Count; } }
        public float Progress
        {
            get { return parts.Count == 0 ? 0f : (float)_studied.Count / parts.Count; }
        }

        bool _running;
        bool _completionAnnounced;
        SelectablePart _openPart;
        Coroutine _dwellRoutine;

        // ---------------------------------------------------------------

        public void Begin()
        {
            CollectParts();

            _studied.Clear();
            _completionAnnounced = false;
            foreach (var p in parts) if (p != null) p.ResetState();

            _running = true;
            RaiseProgress();
            RefreshHint();
        }

        public void Stop()
        {
            _running = false;
            ClearHints();
            if (_dwellRoutine != null) { StopCoroutine(_dwellRoutine); _dwellRoutine = null; }
        }

        void CollectParts()
        {
            if (parts != null && parts.Count > 0)
            {
                parts.RemoveAll(p => p == null || p.info == null);
                parts.Sort((a, b) => a.info.order.CompareTo(b.info.order));
                return;
            }
            parts = SelectablePart.AllSorted();
        }

        // ---------------------------------------------------------------
        // Opening a component
        // ---------------------------------------------------------------

        /// <summary>
        /// Called when the user taps a component and its panel opens.
        ///
        /// A short dwell is required before it counts, so a stray tap while rotating
        /// the model does not silently tick something off a checklist the user has not
        /// actually read.
        /// </summary>
        public void OnPartOpened(SelectablePart part)
        {
            if (!_running || part == null || part.info == null) return;

            _openPart = part;

            if (_dwellRoutine != null) StopCoroutine(_dwellRoutine);
            if (_studied.Contains(part.PartId)) { RefreshHint(); return; }

            _dwellRoutine = StartCoroutine(DwellThenCredit(part));
        }

        public void OnPartClosed()
        {
            _openPart = null;
            if (_dwellRoutine != null) { StopCoroutine(_dwellRoutine); _dwellRoutine = null; }
        }

        IEnumerator DwellThenCredit(SelectablePart part)
        {
            float t = 0f;
            while (t < dwellToCredit)
            {
                if (_openPart != part) yield break;     // user moved on before it counted
                t += Time.deltaTime;
                yield return null;
            }

            MarkStudied(part);
            _dwellRoutine = null;
        }

        /// <summary>
        /// The only place a component becomes studied. Everything else routes here, so
        /// the count and the UI cannot drift apart.
        /// </summary>
        public void MarkStudied(SelectablePart part)
        {
            if (part == null || part.info == null) return;
            if (!_studied.Add(part.PartId)) return;      // already counted

            part.MarkStudied();

            AudioDirector.Play("ui_check");
            Haptics.Confirm();

            if (PartStudied != null) PartStudied(part);
            RaiseProgress();
            RefreshHint();

            if (IsComplete && !_completionAnnounced)
            {
                _completionAnnounced = true;
                StartCoroutine(AnnounceCompletion());
            }
        }

        IEnumerator AnnounceCompletion()
        {
            yield return new WaitForSeconds(0.45f);
            ClearHints();
            AudioDirector.Play("tour_complete");
            Haptics.Impact();
            if (TourCompleted != null) TourCompleted();
        }

        void RaiseProgress()
        {
            if (ProgressChanged != null) ProgressChanged(_studied.Count, parts.Count);
        }

        public bool IsStudied(string partId)
        {
            return _studied.Contains(partId);
        }

        public bool IsStudied(SelectablePart part)
        {
            return part != null && part.info != null && _studied.Contains(part.PartId);
        }

        // ---------------------------------------------------------------
        // Hinting
        // ---------------------------------------------------------------

        /// <summary>The next unstudied component in tour order, or null if done.</summary>
        public SelectablePart NextSuggested()
        {
            return NextSuggested(null);
        }

        /// <summary>
        /// The next unstudied component, skipping <paramref name="exclude"/> - the one
        /// currently open, which is about to be credited. Without the exclusion the
        /// "Next system" button vanished whenever the open part was itself the
        /// suggested one, which in tour order is nearly always.
        /// </summary>
        public SelectablePart NextSuggested(SelectablePart exclude)
        {
            foreach (var p in parts)
            {
                if (p == null || p.info == null || p == exclude) continue;
                if (!_studied.Contains(p.PartId)) return p;
            }
            return null;
        }

        void RefreshHint()
        {
            ClearHints();
            if (!hintNextPart || !_running) return;

            var next = NextSuggested();
            if (next != null) next.SetHinted(true);
        }

        void ClearHints()
        {
            foreach (var p in parts) if (p != null) p.SetHinted(false);
        }

        // ---------------------------------------------------------------
        // Narration
        // ---------------------------------------------------------------

        public void PlayNarration(PartInfo info)
        {
            if (narrationSource == null || info == null || info.narration == null) return;
            if (AppDirector.Instance != null && !AppDirector.Instance.SoundEnabled) return;

            narrationSource.Stop();
            narrationSource.clip = info.narration;
            narrationSource.Play();
        }

        public void StopNarration()
        {
            if (narrationSource != null) narrationSource.Stop();
        }

        // ---------------------------------------------------------------
        // Debug / demo helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Credits everything at once. Bound to a long-press on the progress ring so a
        /// presenter can jump to the quiz without working through all 17 components
        /// while an audience waits.
        /// </summary>
        public void CompleteAll()
        {
            foreach (var p in parts)
            {
                if (p == null || p.info == null) continue;
                if (_studied.Add(p.PartId)) p.MarkStudied();
            }

            RaiseProgress();
            ClearHints();

            if (!_completionAnnounced)
            {
                _completionAnnounced = true;
                StartCoroutine(AnnounceCompletion());
            }
        }
    }
}
