using System;
using DomainBridge;

namespace DomainBridge.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DomainBridge Sample Application");
            Console.WriteLine("==============================\n");
            
            try
            {
                // Simply use the bridge class directly!
                var app = ThirdPartyApplicationBridge.Instance;
                
                Console.WriteLine("Using ThirdPartyApplicationBridge.Instance");
                Console.WriteLine($"Initial Status: {app.Status}");
                
                // Connect to server
                app.Connect("localhost", 8080);
                Console.WriteLine($"After Connect: {app.Status}");
                
                if (app.IsConnected)
                {
                    // Get database - returns DatabaseBridge automatically
                    var db = app.GetDatabase(1);
                    Console.WriteLine($"Got database: {db.Name}");
                    
                    // Get document - returns DocumentBridge automatically
                    var doc = db.GetDocument("sample-doc");
                    Console.WriteLine($"Document: {doc.Title}");
                    Console.WriteLine($"Content: {doc.Content}");
                    
                    app.Disconnect();
                    Console.WriteLine($"After Disconnect: {app.Status}");
                }
                
                // Example with custom configuration
                Console.WriteLine("\nCreating with custom configuration:");
                var customApp = ThirdPartyApplicationBridge.CreateIsolated(new DomainConfiguration
                {
                    PrivateBinPath = "ThirdPartyLibs",
                    EnableShadowCopy = true
                });
                Console.WriteLine($"Custom app status: {customApp.Status}");
                
                // Clean up the isolated domain
                ThirdPartyApplicationBridge.UnloadDomain();
                Console.WriteLine("\nSuccessfully unloaded domain.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
    
    // Define a bridge class for the third-party application
    [DomainBridge(typeof(ThirdPartyApplication), 
        PrivateBinPath = "ThirdPartyLibs",
        EnableShadowCopy = true)]
    public partial class ThirdPartyApplicationBridge
    {
        // The source generator will generate:
        // - Static Instance property (matching ThirdPartyApplication.Instance)
        // - All properties: IsConnected, Status
        // - All methods: Connect(), Disconnect(), GetDatabase()
        // - Automatic proxying for returned types (Database → DatabaseBridge)
        // - Static CreateIsolated() and UnloadDomain() methods
        // - AppDomain configuration using the specified PrivateBinPath and shadow copying
    }
    
    // Simulated third-party types (would normally be in external assembly)
    public class ThirdPartyApplication
    {
        public static ThirdPartyApplication Instance { get; } = new ThirdPartyApplication();
        
        public bool IsConnected { get; private set; }
        public string Status { get; private set; } = "Disconnected";
        
        public void Connect(string server, int port)
        {
            IsConnected = true;
            Status = $"Connected to {server}:{port}";
            Console.WriteLine($"[ThirdParty] Connected to {server}:{port}");
        }
        
        public void Disconnect()
        {
            IsConnected = false;
            Status = "Disconnected";
            Console.WriteLine("[ThirdParty] Disconnected");
        }
        
        public Database GetDatabase(int id)
        {
            Console.WriteLine($"[ThirdParty] Getting database {id}");
            return new Database { Id = id, Name = $"Database_{id}" };
        }
    }
    
    public class Database
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        
        public Document GetDocument(string id)
        {
            Console.WriteLine($"[ThirdParty] Getting document {id}");
            return new Document 
            { 
                Id = id, 
                Title = $"Document {id}",
                Content = "This is sample content from the isolated domain."
            };
        }
    }
    
    public class Document
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }
}