using System.Text.Json.Serialization;

namespace StorySpoiler_BNY24.Models
{
    public class ApiResponseDTO
    {

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("storyId")]
        public string? StoryId { get; set; }

    }
}
