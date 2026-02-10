using System.Text.Json;
using V6502;
using Velutia.Models;

namespace Velutia;

internal static class Program
{
    private static void Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var testsDirectory = Path.Join(currentDirectory, "/SingleStepTests/");
        var filename = "ea.json";
        var testPath = Path.Join(testsDirectory, filename);
        var jsonString = File.ReadAllText(testPath);

        var singleStepTest = JsonSerializer.Deserialize<List<SingleStepTest>>(jsonString)!;

        foreach (var test in singleStepTest)
        {
            var initialRam = test.Initial.Ram.ToDictionary(ramArray => ramArray[0], ramArray => ramArray[1]);
            var cpu = new Cpu(test.Initial.Pc, test.Initial.S, test.Initial.A, test.Initial.X, test.Initial.Y,
                test.Initial.P, initialRam);

            cpu.Start();

            if (CompareRegisters(cpu, test) && CompareMemory(cpu, test))
            {
                Console.WriteLine("Registers + memory are equal!");
            }
        }
    }

    private static bool CompareRegisters(Cpu cpu, SingleStepTest test)
    {
        return cpu.Pc == test.Final.Pc && cpu.Sp == test.Final.S && cpu.Ac == test.Final.A && cpu.X == test.Final.X &&
               cpu.Y == test.Final.Y && cpu.P == test.Final.P;
    }

    private static bool CompareMemory(Cpu cpu, SingleStepTest test)
    {
        var finalRam = test.Final.Ram.ToDictionary(ramArray => ramArray[0], ramArray => ramArray[1]);
        var areEqual = cpu.Ram.OrderBy(kv => kv.Key).SequenceEqual(finalRam.OrderBy(kv => kv.Key));

        return areEqual;
    }

    /*
    private static bool CompareCycles()
    {
        
    } */
}