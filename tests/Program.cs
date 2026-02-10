using System.Text.Json;
using V6502;
using V6502.Memory;
using Velutia.Models;

namespace Velutia;

internal static class Program
{
    private static void Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var testsDirectory = Path.Join(currentDirectory, "/SingleStepTests/");
        var filename = "6c.json";
        var testPath = Path.Join(testsDirectory, filename);
        var jsonString = File.ReadAllText(testPath);

        var singleStepTest = JsonSerializer.Deserialize<List<SingleStepTest>>(jsonString)!;

        var count = 0;

        foreach (var test in singleStepTest)
        {
            var memoryDict = test.Initial.Ram.ToDictionary(ramArray => ramArray[0], ramArray => (byte)ramArray[1]);
            IMemory initialMemoryState = new MemorySst(memoryDict);
            
            var cpu = new Cpu(test.Initial.Pc, test.Initial.S, test.Initial.A, test.Initial.X, test.Initial.Y,
                test.Initial.P, initialMemoryState);

            var finalRam = test.Final.Ram.ToDictionary(ramArray => ramArray[0], ramArray => (byte)ramArray[1]);

            cpu.RunInstruction();
            
            if (CompareRegisters(cpu, test) && CompareMemory(cpu, test, finalRam))
            {
                Console.WriteLine("Registers + memory are equal!");
                Console.WriteLine($"Test Name: {test.Name}, Test #: {count}");
            }
            else
            {
                Console.WriteLine("Registers + memory is not equal!");
                Console.WriteLine($"Test Name: {test.Name}, Test #: {count}");
            }

            count++;
        }
    }

    private static bool CompareRegisters(Cpu cpu, SingleStepTest test)
    {
        return cpu.Pc == test.Final.Pc && cpu.Sp == test.Final.S && cpu.Ac == test.Final.A && cpu.X == test.Final.X &&
               cpu.Y == test.Final.Y && cpu.P == test.Final.P;
    }

    private static bool CompareMemory(Cpu cpu, SingleStepTest test, Dictionary<ushort, byte> finalRam)
    {
        foreach (var key in finalRam.Keys)
        {
            var emulatorValue = cpu.Memory.Read(key);

            if (emulatorValue != finalRam[key])
            {
                return false;
            }
        }
        
        return true;
    }
}