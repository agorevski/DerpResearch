using System.Text;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;

namespace DeepResearch.WebApp.Agents;

internal static class ContextSummary
{
    public static string Build(ConversationContext context, bool includeMemories)
    {
        if (context.RecentMessages.Length == 0 &&
            (!includeMemories || context.RelevantMemories.Length == 0))
        {
            return "";
        }

        var summary = new StringBuilder("Previous Context:\n");

        if (context.RecentMessages.Length > 0)
        {
            summary.Append("Recent conversation:\n");
            foreach (var msg in context.RecentMessages.TakeLast(3))
            {
                summary.Append($"- {msg.Role}: {TextHelper.Truncate(msg.Content, 100)}...\n");
            }
        }

        if (includeMemories && context.RelevantMemories.Length > 0)
        {
            summary.Append("\nRelevant memories:\n");
            foreach (var mem in context.RelevantMemories.Take(3))
            {
                summary.Append($"- {TextHelper.Truncate(mem.Text, 100)}...\n");
            }
        }

        return summary.ToString();
    }
}
