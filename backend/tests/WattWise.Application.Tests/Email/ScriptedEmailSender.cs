using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

/// <summary>A hand-written sender: answers per recipient address (default: sent), or throws, and writes every call to the shared call log.</summary>
internal sealed class ScriptedEmailSender(List<string> calls) : IEmailSender
{
    private readonly Dictionary<string, Func<SendResult>> _byRecipient = [];

    /// <summary>Runs on every send before the answer, such as advancing the clock the way a slow server would.</summary>
    public Action? OnSend { get; set; }

    public List<(EmailMessage Message, string MessageIdLocalPart)> Sent { get; } = [];

    public void Answer(string recipient, SendResult result) => _byRecipient[recipient] = () => result;

    public void Throw(string recipient, Exception exception) => _byRecipient[recipient] = () => throw exception;

    public Task<SendResult> SendAsync(EmailMessage message, string messageIdLocalPart, CancellationToken cancellationToken)
    {
        calls.Add("send");
        Sent.Add((message, messageIdLocalPart));
        OnSend?.Invoke();
        return Task.FromResult(_byRecipient.TryGetValue(message.To, out Func<SendResult>? answer) ? answer() : SendResult.Sent);
    }
}
