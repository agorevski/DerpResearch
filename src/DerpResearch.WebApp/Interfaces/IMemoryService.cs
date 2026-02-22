namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Façade interface combining all memory-related operations.
/// Maintained for backward compatibility. New consumers should depend on
/// the specific interface they need: IMemoryStorage, IConversationManager, or IClarificationStorage.
/// </summary>
public interface IMemoryService : IMemoryStorage, IConversationManager, IClarificationStorage
{
}
