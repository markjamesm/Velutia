using System.Text.Json;
using V6502;
using Velutia.Models;

namespace Velutia;

internal class Program
{
    private static void Main(string[] args)
    {
        var filepath = GetFilepath("a5.json");

        var jsonString = File.ReadAllText(filepath);
        var singleStepTest = JsonSerializer.Deserialize<List<SingleStepTest>>(jsonString)!;

        var count = 1;
        
        foreach (var test in singleStepTest)
        {
            var testMemory = new Memory(PopulateCpuMemory(test.Initial.Ram, new byte[65536]));
            
            var registers = new Registers(test.Initial.Pc, test.Initial.S, test.Initial.A, test.Initial.X, test.Initial.Y,
                test.Initial.P);
            
            var cpu = new Cpu(registers, testMemory);
            
            cpu.RunInstruction();
            
            CompareResults(cpu, test);

         //   Console.WriteLine($"Iteration: {count}");
         //   count++;
            
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
           // PrintComparison(cpu, test);
          //  Console.WriteLine("Registers + memory are equal!");
           // Console.WriteLine("Test passed");
        }
        else
        {
            PrintComparison(cpu, test);
            Console.WriteLine("*** Registers + memory are not equal! ***");
        }
    }
    
    private static bool CompareRegisters(Cpu cpu, SingleStepTest test)
    {
        return cpu.Registers.Pc == test.Final.Pc && cpu.Registers.S == test.Final.S && cpu.Registers.A == test.Final.A && cpu.Registers.X == test.Final.X &&
               cpu.Registers.Y == test.Final.Y && cpu.Registers.P == test.Final.P;
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
            $"Expected registers: " +
            $"A:{test.Final.A:X2} " +
            $"X:{test.Final.X:X2} " +
            $"Y:{test.Final.Y:X2} " +
            $"S:{test.Final.S:X2} " +
            $"P:{test.Final.P:X2} " +
            $"PC:{test.Final.Pc:X4}");
        
        Console.WriteLine(
            $"Actual registers: " +
            $"A:{cpu.Registers.A:X2} " +
            $"X:{cpu.Registers.X:X2} " +
            $"Y:{cpu.Registers.Y:X2} " +
            $"S:{cpu.Registers.S:X2} " +
            $"P:{cpu.Registers.P:X2} " +
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
            Console.Write($"{row[0]:X4}:{cpu.Memory.Read(row[0]):X2} ");
        }
        
        Console.WriteLine();
        Console.WriteLine();
    }
} 