using System.Text.Json;

namespace WattWise.Testing;

/// <summary>Reads caught email back from a Mailpit container's HTTP API.</summary>
public sealed class MailpitClient(Uri baseAddress) : IDisposable
{
    private readonly HttpClient http = new() { BaseAddress = baseAddress };

    /// <summary>Waits until a message to <paramref name="recipient"/> arrives and returns it.</summary>
    public async Task<MailpitMessage> WaitForMessageToAsync(string recipient, CancellationToken cancellationToken)
    {
        string path = $"api/v1/search?query={Uri.EscapeDataString($"to:\"{recipient}\"")}";
        string id = await Poll.UntilAsync(
            () => FirstMessageId(path, cancellationToken),
            $"a message to {recipient} in Mailpit",
            cancellationToken,
            TimeSpan.FromSeconds(15));

        return (await http.GetFromJsonAsync<MailpitMessage>($"api/v1/message/{id}", JsonSerializerOptions.Web, cancellationToken))!;
    }

    public void Dispose() => http.Dispose();

    // Poll takes a synchronous probe; the search is one quick local call.
    private string? FirstMessageId(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = http.GetAsync(path, cancellationToken).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(response.Content.ReadAsStream(cancellationToken));
        JsonElement messages = document.RootElement.GetProperty("messages");
        return messages.GetArrayLength() == 0 ? null : messages[0].GetProperty("ID").GetString();
    }
}
