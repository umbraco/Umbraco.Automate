using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;

/// <summary>
/// The single place that turns a step type's outcome list into response models. The catalogue
/// (static outcomes) and the resolve endpoint (dynamic outcomes) both use it so they cannot drift.
/// </summary>
internal static class StepOutcomeResponseMapping
{
    /// <summary>
    /// Maps an outcome list from a (possibly third-party) step type. A null list maps to none, and
    /// null items are skipped, each with a warning naming the alias, so a misbehaving step type
    /// never takes the caller down. <paramref name="map"/> is the registered
    /// <c>StepOutcome</c> to <c>StepOutcomeResponseModel</c> map, from either an <c>IUmbracoMapper</c> or a <c>MapperContext</c>.
    /// </summary>
    public static List<StepOutcomeResponseModel> MapOutcomes(
        Func<StepOutcome, StepOutcomeResponseModel> map,
        ILogger logger,
        string alias,
        IReadOnlyList<StepOutcome>? outcomes)
    {
        if (outcomes is null)
        {
            logger.LogWarning("Step type '{Alias}' returned no outcome list; exposing none.", alias);
            return [];
        }

        var models = new List<StepOutcomeResponseModel>(outcomes.Count);
        for (var index = 0; index < outcomes.Count; index++)
        {
            StepOutcome? outcome = outcomes[index];
            if (outcome is null)
            {
                logger.LogWarning("Step type '{Alias}' returned a null outcome at position {Position}; skipping it.", alias, index);
                continue;
            }

            models.Add(map(outcome));
        }

        return models;
    }
}
