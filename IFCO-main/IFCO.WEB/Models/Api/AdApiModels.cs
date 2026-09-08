using System.Text.Json.Serialization;

public record UserRequest(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("password")] string P
);

public record UserDetails(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("user_name")] string UserName,
    [property: JsonPropertyName("validation_status")] string ValidationStatus
);