using System.Text.Json;
using V6502;
using Velutia.Models;

namespace Velutia;

internal class Program
{
    private static void Main(string[] args)
    {
        var filepath = GetFilepath("6c.json");

        var jsonString = File.ReadAllText(filepath);
        var singleStepTest = JsonSerializer.Deserialize<List<SingleStepTest>>(jsonString)!;

        foreach (var test in singleStepTest)
        {
            var testMemory = new Memory(PopulateCpuMemory(test.Initial.Ram, new byte[ushort.MaxValue]));
            
            var cpu = new Cpu(test.Initial.Pc, test.Initial.S, test.Initial.A, test.Initial.X, test.Initial.Y,
                test.Initial.P, testMemory);
            
            cpu.RunInstruction();
            
            CompareResults(cpu, test);
        }
    }

    private static string GetFilepath(string filename)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var testsDirectory = Path.Join(currentDirectory, "/SingleStepTests/");

        return Path.Join(testsDirectory, filename);
    }

    private static byte[] PopulateCpuMemory(ushort[][] initialMemory, byte[] memory)
    {
        foreach (var row in initialMemory)
        {
            memory[row[0]] = (byte)row[1];
        }

        return memory;
    }

    private static void CompareResults(Cpu cpu, SingleStepTest test)
    {
        if (CompareRegisters(cpu, test) && CompareMemory(cpu, test))
        {
            PrintComparison(cpu, test);
            Console.WriteLine("Registers + memory are equal!");
        }
        else
        {
            PrintComparison(cpu, test);
            Console.WriteLine("Registers + memory are not equal!");
        }
        
        PrintComparison(cpu, test);
    }
    
    private static bool CompareRegisters(Cpu cpu, SingleStepTest test)
    {
        return cpu.Pc == test.Final.Pc && cpu.S == test.Final.S && cpu.A == test.Final.A && cpu.X == test.Final.X &&
               cpu.Y == test.Final.Y && cpu.P == test.Final.P;
    }

    private static bool CompareMemory(Cpu cpu, SingleStepTest test)
    {
        foreach (var row in test.Final.Ram)
        {
            if (cpu.Memory.Read(row[0]) != row[1])
            {
                return false;
            }
        }

        return true;
    }

    private static void PrintComparison(Cpu cpu, SingleStepTest test)
    {
        Console.WriteLine("-------------------------------");
        Console.WriteLine($"Test {test.Name}");
        Console.WriteLine(
            $"Initial registers: A:{test.Initial.A:X2} " +
            $"X:{test.Initial.X:X2} " +
            $"Y:{test.Initial.Y:X2} " +
            $"S:{test.Initial.S:X2} " +
            $"P:{test.Initial.P:X2} " +
            $"PC:{test.Initial.Pc:X4}");

        Console.WriteLine(
            $"Actual registers: A: {cpu.A:X2} " +
            $"X:{cpu.X:X2} " +
            $"Y:{cpu.Y:X2} " +
            $"S:{cpu.S:X2} " +
            $"P:{cpu.P:X2} " +
            $"PC:{cpu.Pc:X4}");

        Console.WriteLine(
            $"Expected registers: A:{test.Final.A:X2} " +
            $"X:{test.Final.X:X2} " +
            $"Y:{test.Final.Y:X2} " +
            $"S:{test.Final.S:X2} " +
            $"P:{test.Final.P:X2} " +
            $"PC:{test.Final.Pc:X4}");

        Console.Write("Initial memory: ");
        foreach (var row in test.Initial.Ram)
        {
            Console.Write($"{row[0]:X4}:{row[1]:X2} ");
        }
        
        Console.WriteLine();
        
        Console.Write("Actual memory: ");
        foreach (var row in test.Final.Ram)
        {
            Console.Write($"{row[0]:X4}:{cpu.Memory.Read(row[0]):X2} ");
        }
        
        Console.WriteLine();
        Console.Write("Final memory: ");
        foreach (var row in test.Final.Ram)
        {
            Console.Write($"{row[0]:X4}:{row[1]:X2} ");
        }
        
        Console.WriteLine();
    }
} 