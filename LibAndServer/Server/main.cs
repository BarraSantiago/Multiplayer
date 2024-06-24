namespace Server;

public class main
{
    private static Manager _manager = new Manager();
    public static void Main(string[] args)
    {
        Console.Write("Please enter an integer: ");
        string userInput = Console.ReadLine();

        int number;
        bool success = int.TryParse(userInput, out number);

        if (success)
        {
            Console.WriteLine($"You entered the integer: {number}");
        }
        else
        {
            Console.WriteLine("Invalid input. Please enter an integer.");
        }
        Console.Write("Please enter your input: ");
        
        //_manager.Start();
    }
}