public interface ITrainingCurriculum
{
    EpisodeSpecification CreateInitialEpisode(EnvironmentInstance environment);
    EpisodeSpecification CreateNextEpisode(EnvironmentInstance environment, EpisodeOutcome previousOutcome);
}
