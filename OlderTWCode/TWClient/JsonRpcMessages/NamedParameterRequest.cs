using StreamJsonRpc;

namespace TWClients.JsonRpcMessages;

public class NamedParameterRequest<TResult>(
    string methodName,
    string[]? requiredParameterNames,
    string[]? optionalParameterNames)
    : OutboundRequest<TResult>(methodName)
{
    public string[]? OptionalParameterNames => optionalParameterNames;
    public string[]? RequiredParameterNames => requiredParameterNames;

    public void Validate(Dictionary<string, object?> parameters)
    {
        foreach (var req in RequiredParameterNames)
            if (!parameters.ContainsKey(req))
                throw new JsonRpcRuntimeException.ActualParameterException("missing required parameter name " + req);

        foreach (var s in parameters.Keys)
        {
            var isKnown = false;
            if (RequiredParameterNames != null)
                if (RequiredParameterNames.Any(p => s == p))
                    isKnown = true;

            if (isKnown) continue;
            if (OptionalParameterNames != null)
                if (OptionalParameterNames.Any(p => s == p))
                    isKnown = true;

            if (isKnown) continue;
            throw new JsonRpcRuntimeException.ActualParameterException("supplied unknown parameter name " + s);
        }
    }
}