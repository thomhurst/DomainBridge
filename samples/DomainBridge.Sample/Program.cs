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
            
            // Demonstrate the new bridge pattern
            DemonstrateBridgePattern();
            
            Console.WriteLine("\n" + new string('-', 50) + "\n");
            
            // Demonstrate the original pattern
            DemonstrateOriginalPattern();
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        static void DemonstrateBridgePattern()
        {
            Console.WriteLine("=== New Bridge Pattern ===");
            
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
                }
                
                // Clean up the isolated domain
                ThirdPartyApplicationBridge.UnloadDomain();
                Console.WriteLine("Successfully unloaded domain.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        static void DemonstrateOriginalPattern()
        {
            Console.WriteLine("=== Original Factory Pattern ===");
            
            try
            {
                // Configure the isolated domain
                var config = new DomainConfiguration
                {
                    DomainName = "ThirdPartyDomain",
                    PrivateBinPath = "ThirdPartyLibs",
                    EnableShadowCopy = true
                };
                
                // Create proxy using factory
                var app = DomainBridgeFactory.Create<IThirdPartyApplication>(config);
                
                Console.WriteLine("Using DomainBridgeFactory.Create<IThirdPartyApplication>");
                app.Connect("localhost", 8080);
                
                if (app.IsConnected)
                {
                    Console.WriteLine($"Connected! Status: {app.Status}");
                    
                    var db = app.GetDatabase(1);
                    Console.WriteLine($"Got database: {db.Name}");
                    
                    app.Disconnect();
                }
                
                // Clean up
                DomainBridgeFactory.UnloadAll();
                Console.WriteLine("Successfully unloaded all domains.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
    
    // New pattern: Define a bridge class
    [DomainBridge(typeof(ThirdPartyApplication))]
    public partial class ThirdPartyApplicationBridge
    {
        // The source generator will generate:
        // - Static Instance property
        // - All properties and methods from ThirdPartyApplication
        // - Automatic proxying for returned types (Database, Document)
        // - Static UnloadDomain() method
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
        }
        
        public void Disconnect()
        {
            IsConnected = false;
            Status = "Disconnected";
        }
        
        public Database GetDatabase(int id)
        {
            return new Database { Id = id, Name = $"Database_{id}" };
        }
    }
    
    public class Database
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        
        public Document GetDocument(string id)
        {
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
    
    // For the original pattern (generated by source generator)
    [DomainBridge]
    public class OriginalPatternExample
    {
        // This generates IOriginalPatternExample interface
    }
}