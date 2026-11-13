using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Read-only curriculum display and optional fixed-stage inference baseline recorder.
/// The CSV contains one row per completed arena episode, never manual restarts.
/// </summary>
public sealed class TrainingBaselineDashboard : MonoBehaviour
{
    private readonly Queue<EpisodeBaselineMetrics> recent = new();
    private readonly List<EnvironmentEpisodeCoordinator> subscribed = new();
    private CortexPoliceCurriculum curriculum;
    private bool baselineMode;
    private int requestedEpisodes;
    private int completedEpisodes;
    private int displayedDifficulty = -1;
    private SearchLessonKind displayedLesson;
    private string runLabel;
    private string mapLabel;
    private string csvPath;
    private bool csvAvailable;

    public void Configure(CortexPoliceCurriculum activeCurriculum,
        IReadOnlyList<EnvironmentInstance> environments, bool recordBaseline,
        int episodeCount, string label)
    {
        Unsubscribe();
        curriculum = activeCurriculum;
        baselineMode = recordBaseline;
        requestedEpisodes = Mathf.Max(1, episodeCount);
        runLabel = string.IsNullOrWhiteSpace(label) ? "unlabelled-model" : label.Trim();
        mapLabel = environments.Count > 0 ? environments[0].Grid.LayoutId : "unknown";
        completedEpisodes = 0;
        displayedDifficulty = (int)curriculum.CurrentStage * 1000 + curriculum.CurrentDistance;
        displayedLesson = curriculum.CurrentSearchLesson;
        recent.Clear();
        foreach (EnvironmentInstance environment in environments)
        {
            EnvironmentEpisodeCoordinator coordinator = environment.EpisodeCoordinator;
            coordinator.EpisodeMeasured += OnEpisodeMeasured;
            subscribed.Add(coordinator);
        }

        if (!baselineMode) return;
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "TrainingBaselines");
            Directory.CreateDirectory(folder);
            csvPath = Path.Combine(folder,
                $"Cortex_{mapLabel}_{curriculum.CurrentStage}_d{curriculum.CurrentDistance}_" +
                $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(csvPath,
                "model_label,utc,map_id,stage,distance,environment_id,episode_number,success," +
                "hiders_caught,hider_count,physics_steps,step_limit,first_sighting_step," +
                "first_capture_step,sighted_hiders,captures_after_sighting," +
                "mean_sighting_to_capture_steps,visible_physics_steps,reacquisitions," +
                "unique_visited_cells,group_reward,checked_cells,reachable_cells," +
                "seeker_0_travel,seeker_0_visited,seeker_0_turn_in_place," +
                "seeker_0_blocked_forward,seeker_0_captures," +
                "seeker_1_travel,seeker_1_visited,seeker_1_turn_in_place," +
                "seeker_1_blocked_forward,seeker_1_captures," +
                "seeker_0_exclusive_checked,seeker_1_exclusive_checked," +
                "shared_new_checked,rewarded_novel_cells," +
                "seeker_spawn_separation_cells,search_lesson,frontier_inspection_pass," +
                "known_walls,frontiers_inspected,first_frontier_inspection_step\n");
            csvAvailable = true;
            Debug.Log($"[Baseline] Recording {curriculum.CurrentStage} episodes to {csvPath}", this);
        }
        catch (Exception exception)
        {
            csvAvailable = false;
            Debug.LogError($"[Baseline] Could not create CSV: {exception.Message}", this);
        }
    }

    private void OnEpisodeMeasured(EpisodeBaselineMetrics measurement)
    {
        if (measurement.Outcome.WasInterrupted ||
            (baselineMode && completedEpisodes >= requestedEpisodes)) return;
        // An arena may finish an older lesson after the shared stage changes.
        int currentDifficulty = (int)curriculum.CurrentStage * 1000 + curriculum.CurrentDistance;
        if (measurement.Outcome.Difficulty != currentDifficulty ||
            measurement.Outcome.SearchLesson != curriculum.CurrentSearchLesson) return;
        if (displayedDifficulty != currentDifficulty || displayedLesson != curriculum.CurrentSearchLesson)
        {
            displayedDifficulty = currentDifficulty;
            displayedLesson = curriculum.CurrentSearchLesson;
            recent.Clear();
        }

        completedEpisodes++;
        recent.Enqueue(measurement);
        while (recent.Count > 100) recent.Dequeue();
        if (baselineMode && csvAvailable)
        {
            try { File.AppendAllText(csvPath, ToCsvRow(measurement)); }
            catch (Exception exception)
            {
                csvAvailable = false;
                Debug.LogError($"[Baseline] Could not write CSV: {exception.Message}", this);
            }
        }
        if (baselineMode && completedEpisodes == requestedEpisodes)
        {
            Time.timeScale = 0f;
            Debug.Log($"[Baseline] {requestedEpisodes} completed {curriculum.CurrentStage} episodes. " +
                $"Simulation paused. CSV: {csvPath}", this);
        }
    }

    private string ToCsvRow(EpisodeBaselineMetrics m)
    {
        EpisodeOutcome outcome = m.Outcome;
        string phase = ((CortexPoliceCurriculum.Stage)(outcome.Difficulty / 1000)).ToString();
        string mean = m.MeanSightingToCaptureSteps >= 0f
            ? m.MeanSightingToCaptureSteps.ToString("F2", CultureInfo.InvariantCulture) : "";
        string escapedLabel = "\"" + runLabel.Replace("\"", "\"\"") + "\"";
        return string.Join(",", escapedLabel, DateTime.UtcNow.ToString("O"), m.MapId, phase,
            (outcome.Difficulty % 1000).ToString(), outcome.EnvironmentId.ToString(),
            outcome.EpisodeNumber.ToString(), outcome.WasSuccessful ? "1" : "0",
            outcome.HidersCaught.ToString(), outcome.HiderCount.ToString(),
            outcome.Steps.ToString(), m.StepLimit.ToString(),
            m.FirstSightingStep >= 0 ? m.FirstSightingStep.ToString() : "",
            m.FirstCaptureStep >= 0 ? m.FirstCaptureStep.ToString() : "",
            m.SightedHiders.ToString(), m.CapturesAfterSighting.ToString(), mean,
            m.VisiblePhysicsSteps.ToString(), m.Reacquisitions.ToString(),
            m.DistinctVisitedCells.ToString(),
            outcome.GroupReward.ToString("F4", CultureInfo.InvariantCulture),
            m.CheckedCells.ToString(), m.ReachableCells.ToString(),
            SeekerColumns(m, 0), SeekerColumns(m, 1),
            m.SeekerExclusiveCheckedCells.Length > 0 ?
                m.SeekerExclusiveCheckedCells[0].ToString() : "",
            m.SeekerExclusiveCheckedCells.Length > 1 ?
                m.SeekerExclusiveCheckedCells[1].ToString() : "",
            m.SharedNewCheckedCells.ToString(),
            m.RewardedNovelCells.ToString(),
            m.SeekerSpawnSeparationCells.ToString(),
            outcome.SearchLesson.ToString(),
            outcome.FrontierInspectionPassed ? "1" : "0",
            m.KnownWalls.ToString(), m.FrontiersInspected.ToString(),
            m.FirstFrontierInspectionStep >= 0 ?
                m.FirstFrontierInspectionStep.ToString() : "") + "\n";
    }

    private static string SeekerColumns(EpisodeBaselineMetrics m, int index)
    {
        if (index >= m.SeekerTravelDistance.Length) return ",,,,";
        return string.Join(",",
            m.SeekerTravelDistance[index].ToString("F3", CultureInfo.InvariantCulture),
            m.SeekerVisitedCells[index].ToString(),
            m.SeekerTurnInPlaceSteps[index].ToString(),
            m.SeekerBlockedForwardDecisions[index].ToString(),
            m.SeekerCaptures[index].ToString());
    }

    private void OnGUI()
    {
        if (curriculum == null) return;
        int currentDifficulty = (int)curriculum.CurrentStage * 1000 + curriculum.CurrentDistance;
        if (displayedDifficulty != currentDifficulty || displayedLesson != curriculum.CurrentSearchLesson)
        {
            displayedDifficulty = currentDifficulty;
            displayedLesson = curriculum.CurrentSearchLesson;
            recent.Clear();
        }
        const float width = 480f;
        const float height = 420f;
        Rect panel = new(12f, 12f, width, height);
        GUI.Box(panel, "Cortex police training");
        float y = 42f;
        DrawLine($"Stage: {curriculum.CurrentStage}    Distance: {curriculum.CurrentDistance} cells", ref y);
        if (curriculum.CurrentStage == CortexPoliceCurriculum.Stage.Search)
            DrawLine($"Search lesson: {curriculum.CurrentSearchLesson}", ref y);
        DrawLine(baselineMode
            ? $"Fixed-stage baseline: {completedEpisodes}/{requestedEpisodes} episodes"
            : $"Advancement: {curriculum.WindowCount}/{curriculum.WindowSize} " +
              $"at {curriculum.WindowSuccessRate:P0} (target {curriculum.SuccessThreshold:P0})", ref y);
        if (baselineMode) DrawLine($"Map: {mapLabel}", ref y);
        else
            foreach (string mapStatus in curriculum.MapWindowStatus.Split(
                         new[] { "  " }, StringSplitOptions.RemoveEmptyEntries))
                DrawLine(mapStatus, ref y);

        int count = recent.Count;
        int wins = 0;
        int sighted = 0;
        int steps = 0;
        int firstSightSum = 0;
        int firstSightCount = 0;
        int chaseSum = 0;
        int chaseCount = 0;
        float checkedFractionSum = 0f;
        int exclusive0 = 0;
        int exclusive1 = 0;
        int shared = 0;
        int emptyCases = 0;
        int emptyPasses = 0;
        int frontierCases = 0;
        int frontierPasses = 0;
        int occupiedCases = 0;
        int occupiedPasses = 0;
        foreach (EpisodeBaselineMetrics result in recent)
        {
            if (result.Outcome.WasSuccessful)
            {
                wins++;
                steps += result.Outcome.Steps;
            }
            if (result.FirstSightingStep >= 0)
            {
                sighted++;
                firstSightSum += result.FirstSightingStep;
                firstSightCount++;
            }
            chaseSum += result.SightingToCaptureStepSum;
            chaseCount += result.CapturesAfterSighting;
            checkedFractionSum += result.CheckedFraction;
            if (result.SeekerExclusiveCheckedCells.Length > 0)
                exclusive0 += result.SeekerExclusiveCheckedCells[0];
            if (result.SeekerExclusiveCheckedCells.Length > 1)
                exclusive1 += result.SeekerExclusiveCheckedCells[1];
            shared += result.SharedNewCheckedCells;
            if (result.Outcome.SearchLesson == SearchLessonKind.Frontier)
            { frontierCases++; if (result.Outcome.FrontierInspectionPassed) frontierPasses++; }
            else if (result.Outcome.SearchLesson == SearchLessonKind.EmptyArea)
            { emptyCases++; if (result.Outcome.FrontierInspectionPassed) emptyPasses++; }
            else if (result.Outcome.SearchLesson == SearchLessonKind.OccupiedArea)
            { occupiedCases++; if (result.Outcome.WasSuccessful) occupiedPasses++; }
        }
        DrawLine($"Recent {count}: full captures {(count > 0 ? (float)wins / count : 0f):P0}", ref y);
        string firstSightText = firstSightCount > 0
            ? ((float)firstSightSum / firstSightCount).ToString("F0") : "n/a";
        string chaseText = chaseCount > 0
            ? ((float)chaseSum / chaseCount).ToString("F0") : "n/a";
        string successTimeText = wins > 0 ? ((float)steps / wins).ToString("F0") : "n/a";
        DrawLine($"Sighting rate {(count > 0 ? (float)sighted / count : 0f):P0}; " +
                 $"first sight {firstSightText} steps", ref y);
        DrawLine($"Sight to capture {chaseText} steps; " +
                 $"success time {successTimeText} steps", ref y);
        DrawLine($"Reachable floor checked {(count > 0 ? checkedFractionSum / count : 0f):P0}", ref y);
        DrawLine($"New cells/episode: S0 {exclusive0 / Mathf.Max(1, count)}, " +
                 $"S1 {exclusive1 / Mathf.Max(1, count)}, shared {shared / Mathf.Max(1, count)}", ref y);
        if (curriculum.CurrentStage == CortexPoliceCurriculum.Stage.Search)
            DrawLine($"Search pass: frontier {frontierPasses}/{frontierCases}, " +
                     $"team {emptyPasses}/{emptyCases}, " +
                     $"occupied {occupiedPasses}/{occupiedCases}", ref y);
        if (baselineMode)
            DrawLine(completedEpisodes >= requestedEpisodes ? "Complete: simulation paused" :
                $"Model label: {runLabel}", ref y);
        else
            DrawLine("Metrics also appear under Baseline/<stage> in TensorBoard", ref y);

        if (baselineMode)
            GUI.Label(new Rect(20f, 392f, width - 16f, 25f),
                csvAvailable ? "CSV: " + csvPath : "CSV unavailable; check Console");
    }

    private static void DrawLine(string value, ref float y)
    {
        GUI.Label(new Rect(20f, y, 465f, 23f), value);
        y += 25f;
    }

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        foreach (EnvironmentEpisodeCoordinator coordinator in subscribed)
            coordinator.EpisodeMeasured -= OnEpisodeMeasured;
        subscribed.Clear();
    }
}
