using M65Converter.Runners;

namespace M65Converter.Sources.Runners.Helpers;

/// <summary>
/// This is where all <see cref="BaseRunner"/>s are registered.
/// 
/// Also provides method for validating runner positions.
/// </summary>
public class RunnersRegister
{
    private List<BaseRunner> Runners { get; } = new();

    #region Adding

    /// <summary>
    /// Adds the given runner to the end of runners list.
    /// </summary>
    public void Register(BaseRunner runner)
    {
        Runners.Add(runner);
    }

    #endregion

    #region Accessing

    /// <summary>
    /// Enumerates all registered runners and calls the given action with each one, in the order in which they were registered.
    /// </summary>
    public void Enumerate(Action<BaseRunner> action)
    {
        foreach (var runner in Runners)
        {
            action(runner);
        }
    }

    #endregion

    #region Validating

    /// <summary>
    /// Validates that up to the given number of runners of given type are registered.
    /// </summary>
    public void ValidateMaxInstances(BaseRunner runner, int max, Func<int, string>? errorDescription = null)
    {
        var count = 0;

        foreach (var registered in Runners)
        {
            if (registered.GetType() == runner.GetType())
            {
                count++;
            }
        }

        if (count > max)
        {
            var message = errorDescription?.Invoke(count)
                ?? $"{max} {runner.GetType().Name} are allowed, {count} found";

            throw new InvalidDataException(message);
        }
    }

    /// <summary>
    /// Validates that the given runner is positioned maximum at the given position (0 means it needs to be on the first position.
    /// </summary>
    public void ValidatePosition(BaseRunner runner, int max, Func<int, string>? errorDescription = null)
    {
        var position = Runners.IndexOf(runner);

        if (position > max)
        {
            var message = errorDescription?.Invoke(position)
                ?? $"{runner.GetType().Name} is allowed on max {max} position, found at {position}";

            throw new InvalidDataException(message);
        }
    }

    /// <summary>
    /// Validates that the all instances of the given <paramref name="runner"/> are positioned AFTER all instances of <paramref name="before"/>.
    /// </summary>
    public void ValidatePositionAfter(BaseRunner runner, Type before, Func<int, int, string>? errorDescription = null)
    {
        // Find the index of given runner.
        var indexOfRunner = Runners.IndexOf(runner);

        // Find the index of last runner that needs to be before the given one.
        var indexOfLastRequisite = -1;
        for (int i = 0; i < Runners.Count; i++)
        {
            if (Runners[i].GetType() == before.GetType())
            {
                indexOfLastRequisite = i;
            }
        }

        if (indexOfLastRequisite > indexOfRunner)
        {
            var message = errorDescription?.Invoke(indexOfRunner, indexOfLastRequisite)
                ?? $"Found {before.Name} at {indexOfLastRequisite} after {runner.GetType().Name} at {indexOfRunner}";

            throw new InvalidDataException(message);
        }
    }

    #endregion
}
