using System.Collections.Concurrent;
using System.Diagnostics;

namespace WattWise.Infrastructure.Tests.Email;

/// <summary>Keeps everything a finished span says (name, status, tags and events) as text, so a test can search it.</summary>
internal sealed class ConcurrentSpanText
{
    private readonly ConcurrentQueue<string> _spans = new();

    public string Everything => string.Join('\n', _spans);

    public void Add(Activity activity) =>
        _spans.Enqueue(string.Join(
            '|',
            [
                activity.DisplayName,
                activity.StatusDescription ?? string.Empty,
                .. activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"),
                .. activity.Events.SelectMany(activityEvent => activityEvent.Tags.Select(tag => $"{tag.Key}={tag.Value}")),
            ]));
}
