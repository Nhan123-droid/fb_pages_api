using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=localhost,1433;Database=master;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
        
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                Console.WriteLine("Connecting to SQL Server...");
                connection.Open();
                Console.WriteLine("Connection successful!");
            }
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}
