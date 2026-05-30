using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string apiKey = "YOUR_GEMINI_API_KEY";
        string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=" + apiKey;
        
        var payload = "{\"contents\":[{\"parts\":[{\"text\":\"Hello\"}]}]}";
        
        using var client = new HttpClient();
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        
        Console.WriteLine("Sending POST to " + url);
        var response = await client.PostAsync(url, content);
        
        Console.WriteLine("Status Code: " + response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine("Response Body: " + body);
    }
}
