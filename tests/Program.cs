using System.Text.Json;
using V6502;
using Velutia.Models;

namespace Velutia;

internal class Program
{
    private static void Main(string[] args)
    {
        var filepath = GetFilepath("e4.json");

        var jsonString = File.ReadAllText(filepath);
        var singleStepTest = JsonSerializer.Deserialize<List<SingleStepTest>>(jsonString)!;
        
        foreach (var test in singleStepTest)
        {
            var testMemory = new Memory(PopulateCpuMemory(test.Initial.Ram, new byte[65536]));
            var testRegisters = new Registers(test.Initial.Pc, test.Initial.S, test.Initial.A, test.Initial.X, test.Initial.Y,
                test.Initial.P);

            var bus = new Bus(testMemory);
            var cpu = new Cpu(testRegisters, bus);
            
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
            Console.WriteLine($"Test {test.Name} Passed!");
            Console.WriteLine("-------------------------------");
        }
        else
        {
            Console.WriteLine($"Test {test.Name} Failed!");
            PrintComparison(cpu, test);
            Console.WriteLine("*** Registers or memory are not equal! ***");
            Console.WriteLine("-------------------------------");
        }
    }
    
    private static bool CompareRegisters(Cpu cpu, SingleStepTest test)
    {
        return cpu.Registers.Pc == test.Final.Pc && cpu.Registers.Sp == test.Final.S && cpu.Registers.A == test.Final.A && cpu.Registers.X == test.Final.X &&
               cpu.Registers.Y == test.Final.Y && cpu.Registers.P == test.Final.P;
    }

    private static bool CompareMemory(Cpu cpu, SingleStepTest test)
    {
        foreach (var row in test.Final.Ram)
        {
            if (cpu.Bus.Read(row[0]) != row[1])
            {
                return false;
            }
        }

        return true;
    }

    private static void PrintComparison(Cpu cpu, SingleStepTest test)
    {
        Console.WriteLine(
            $"Initial registers: " +
            $"A:{test.Initial.A:X2} " +
            $"X:{test.Initial.X:X2} " +
            $"Y:{test.Initial.Y:X2} " +
            $"Sp:{test.Initial.S:X2} " +
            $"P:{test.Initial.P:B8} " +
            $"PC:{test.Initial.Pc:X4}");
        
        Console.WriteLine(
            $"Expected registers: " +
            $"A:{test.Final.A:X2} " +
            $"X:{test.Final.X:X2} " +
            $"Y:{test.Final.Y:X2} " +
            $"Sp:{test.Final.S:X2} " +
            $"P:{test.Final.P:B8} " +
            $"PC:{test.Final.Pc:X4}");
        
        Console.WriteLine(
            $"Actual registers: " +
            $"A:{cpu.Registers.A:X2} " +
            $"X:{cpu.Registers.X:X2} " +
            $"Y:{cpu.Registers.Y:X2} " +
            $"Sp:{cpu.Registers.Sp:X2} " +
            $"P:{cpu.Registers.P:B8} " +
            $"PC:{cpu.Registers.Pc:X4}");
        
        Console.Write("Expected memory: ");
        foreach (var row in test.Final.Ram)
        {
            Console.Write($"{row[0]:X4}:{row[1]:X2} ");
        }
        
        Console.WriteLine();
        
        Console.Write("Actual memory: ");
        foreach (var row in test.Final.Ram)
        {
            Console.Write($"{row[0]:X4}:{cpu.Bus.Read(row[0]):X2} ");
        }
        
        Console.WriteLine();
        Console.WriteLine();
    }
} 