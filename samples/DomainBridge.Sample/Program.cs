using System;
using DomainBridge;

namespace DomainBridge.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DomainBridge Sample Application");
            Console.WriteLine("==============================");
            
            try
            {
                // Configure the isolated domain
                var config = new DomainConfiguration
                {
                    DomainName = "ThirdPartyDomain",
                    PrivateBinPath = "ThirdPartyLibs",
                    EnableShadowCopy = true
                };
                
                // Create proxy
                var app = DomainBridgeFactory.Create<IThirdPartyApplication>(config);
                
                // Use the proxy just like the original
                Console.WriteLine("Connecting to server...");
                app.Connect("localhost", 8080);
                
                if (app.IsConnected)
                {
                    Console.WriteLine($"Connected! Status: {app.Status}");
                    
                    var db = app.GetDatabase(1);
                    Console.WriteLine($"Got database: {db.Name}");
                    
                    var doc = db.GetDocument("sample-doc");
                    Console.WriteLine($"Document: {doc.Title} - {doc.Content}");
                    
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
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
    
    // Example third-party types
    [DomainBridge]
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
}