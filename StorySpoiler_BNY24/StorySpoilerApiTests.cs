using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using System.Net;
using System.Text.Json;
using StorySpoiler_BNY24.Models;

namespace StorySpoiler_BNY24.Tests
{
    [TestFixture]
    public class StorySpoilerApiTests
    {
        private RestClient _client = null!;
        private static string? _storyId;

        
        private const string DefaultBaseUrl = "https://d3s5nxhwblsjbi.cloudfront.net";
        private const string DefaultApiBase = "/api";
        
        private const string DefaultUserName = "billy12";
        private const string DefaultPassword = "billy12billy12";

        
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var baseUrl = System.Environment.GetEnvironmentVariable("BASE_URL") ?? DefaultBaseUrl;
            var apiBase = System.Environment.GetEnvironmentVariable("API_BASE") ?? DefaultApiBase;

            var options = new RestClientOptions(baseUrl + apiBase);
            _client = new RestClient(options);

            
            var userName = System.Environment.GetEnvironmentVariable("STORY_USER") ?? DefaultUserName;
            var password = System.Environment.GetEnvironmentVariable("STORY_PASS") ?? DefaultPassword;

            Assert.That(!string.IsNullOrWhiteSpace(userName), "Username is missing. Set STORY_USER env var or DefaultUserName.");
            Assert.That(!string.IsNullOrWhiteSpace(password), "Password is missing. Set STORY_PASS env var or DefaultPassword.");

            var loginReq = new RestRequest("User/Authentication", Method.Post);
            loginReq.AddJsonBody(new { userName, password });

            var loginResp = _client.Execute(loginReq);
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Login failed: {loginResp.StatusCode} -> {loginResp.Content}");

            
            string? token = null;
            try
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(loginResp.Content!, JsonOpts);
                if (doc.TryGetProperty("accessToken", out var tok))
                    token = tok.GetString();
            }
            catch {  }

            Assert.That(!string.IsNullOrWhiteSpace(token), "No accessToken returned on authentication.");

            
            _client = new RestClient(new RestClientOptions(baseUrl + apiBase)
            {
                Authenticator = new JwtAuthenticator(token!)
            });
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            _client?.Dispose();
        }

        
        [Test, Order(1)]
        public void CreateStory_ShouldReturnCreated_AndIdAndMessage()
        {
            var payload = new StoryDTO
            {
                Title = $"Exam Story {System.DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Description = "Auto-created during exam tests",
                Url = ""
            };

            var req = new RestRequest("Story/Create", Method.Post);
            req.AddJsonBody(payload);

            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Created),
                $"Expected 201 Created, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            var dto = SafeDeserialize<ApiResponseDTO>(resp.Content);
            
            var storyId = dto?.StoryId ?? TryGetJsonString(resp.Content, "storyId") ?? TryGetJsonString(resp.Content, "id");
            Assert.That(!string.IsNullOrWhiteSpace(storyId), "StoryId was not returned in the response.");
            Assert.That(Normalize(dto?.Msg), Is.EqualTo("Successfully created!"), $"Unexpected msg: {resp.Content}");

            _storyId = storyId; 
        }

        
        [Test, Order(2)]
        public void EditStory_ShouldReturnOk_AndMessage()
        {
            Assert.That(!string.IsNullOrWhiteSpace(_storyId), "No StoryId from create step.");

            var payload = new StoryDTO
            {
                Title = "Edited Title",
                Description = "Edited Description",
                Url = ""
            };

            var req = new RestRequest($"Story/Edit/{_storyId}", Method.Put);
            req.AddJsonBody(payload);

            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected 200 OK, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            var dto = SafeDeserialize<ApiResponseDTO>(resp.Content);
            Assert.That(Normalize(dto?.Msg), Is.EqualTo("Successfully edited"), $"Unexpected msg: {resp.Content}");
        }

        
        [Test, Order(3)]
        public void GetAllStories_ShouldReturnOk_AndNonEmptyArray()
        {
            var req = new RestRequest("Story/All", Method.Get);
            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected 200 OK, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            
            var doc = JsonSerializer.Deserialize<JsonElement>(resp.Content!, JsonOpts);
            Assert.That(doc.ValueKind, Is.EqualTo(JsonValueKind.Array), "Response is not an array.");
            Assert.That(doc.GetArrayLength(), Is.GreaterThan(0), "Array is empty.");
        }

        
        [Test, Order(4)]
        public void DeleteStory_ShouldReturnOk_AndDeletedMessage()
        {
            Assert.That(!string.IsNullOrWhiteSpace(_storyId), "No StoryId from create step.");

            var req = new RestRequest($"Story/Delete/{_storyId}", Method.Delete);
            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected 200 OK, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            
            var msg = TryGetJsonString(resp.Content, "msg") ?? Normalize(resp.Content);
            Assert.That(msg, Is.EqualTo("Deleted successfully!"), $"Unexpected delete message: {resp.Content}");
        }

        
        [Test, Order(5)]
        public void CreateStory_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var payload = new StoryDTO { Title = "", Description = "" };

            var req = new RestRequest("Story/Create", Method.Post);
            req.AddJsonBody(payload);

            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                $"Expected 400 BadRequest, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");
        }

        
        [Test, Order(6)]
        public void EditNonExistingStory_ShouldReturnNotFound_AndMessage()
        {
            
            var fakeId = "00000000-0000-0000-0000-000000000000";

            var payload = new StoryDTO
            {
                Title = "Does not matter",
                Description = "Does not matter",
                Url = ""
            };

            var req = new RestRequest($"Story/Edit/{fakeId}", Method.Put);
            req.AddJsonBody(payload);

            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                
                $"Expected 404 NotFound, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            var msg = TryGetJsonString(resp.Content, "msg") ?? Normalize(resp.Content);
            StringAssert.Contains("No spoilers", msg, $"Unexpected message: {resp.Content}");
        }

        
        [Test, Order(7)]
        public void DeleteNonExistingStory_ShouldReturnBadRequest_AndMessage()
        {
            var fakeId = "00000000-0000-0000-0000-000000000000";

            var req = new RestRequest($"Story/Delete/{fakeId}", Method.Delete);
            var resp = _client.Execute(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                $"Expected 400 BadRequest, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {resp.Content}");

            var msg = TryGetJsonString(resp.Content, "msg") ?? Normalize(resp.Content);
            StringAssert.Contains("Unable to delete this story spoiler!", msg, $"Unexpected message: {resp.Content}");
        }

        
        private static T? SafeDeserialize<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
            catch { return default; }
        }

        private static string? TryGetJsonString(string? json, string key)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var el = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var prop))
                    return prop.GetString();
                return null;
            }
            catch { return null; }
        }

        private static string Normalize(string? s)
        {
            if (s == null) return "";
            return s.Trim().Trim('"');
        }
    }
}

