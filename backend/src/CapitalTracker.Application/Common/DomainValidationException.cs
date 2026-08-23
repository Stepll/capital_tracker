namespace CapitalTracker.Application.Common;

/// <summary>
/// A rule the owner can satisfy by typing something else: selling more units than are
/// held, an unsupported currency. Mapped to 400 with its message intact (see
/// DomainValidationExceptionFilter) rather than the 500 a bare exception gives, because
/// the message is the point — the form has to be able to show it.
///
/// Derives from InvalidOperationException so the handlers that already threw that for
/// exactly these cases keep behaving the same for anything catching the base type.
/// </summary>
public class DomainValidationException(string message) : InvalidOperationException(message);
