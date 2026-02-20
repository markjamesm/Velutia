using System.Text.Json;
using NUnit.Framework.Internal;
using Velutia.Cpu.Tests.Models;
using Velutia.Processor;

namespace Velutia.Cpu.Tests;

public class Tests
{
    public record TestName(string FileName, int TestNumber);
    
    [TestCaseSource(nameof(IndividualTestCase))]
    public void TestCpuState(TestName testName)
    {
        var test = GetTest(testName);
        var cpu = CreateCpuAndRun(out var memory, test);

        AssertRegisters(cpu, new Registers(
            test.Final.Pc, test.Final.S, test.Final.A, test.Final.X, test.Final.Y, test.Final.P));

        AssertMemory(memory, test.Final.Ram);
    }
    
    private static IEnumerable<TestName> GetAllJsonFiles()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        var testPath = Path.Combine(testDir, "Data", "SingleStepTests");

        var testNumber = 0;

        foreach (var file in Directory.EnumerateFiles(testPath, "*.json", SearchOption.AllDirectories))
        {
            yield return new TestName(file, testNumber);
            testNumber++;
        }
    }
    
    private static SingleStepTest GetTest(TestName testName)
    {
        using var stream = File.OpenRead(testName.FileName);
        var tests = JsonSerializer.Deserialize<List<SingleStepTest>>(stream)!;
        return tests[testName.TestNumber];
    }

    
    private static IEnumerable<TestName> AllTestCases()
    {
        foreach (var test in GetAllJsonFiles())
        {
            yield return test;
        }
    }

    private static IEnumerable<TestName> IndividualTestCase()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        var testPath = Path.Combine(testDir, "Data", "SingleStepTests");
        var fileName = Path.Combine(testPath, "35.json");
            
        yield return new TestName(fileName, 0);
    }

    /*
    private static IEnumerable<SingleStepTest> IndividualTestCase()
    {
        var filepath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Data/SingleStepTests/35.json");
        var jsonString = File.ReadAllText(filepath);

        return JsonSerializer.Deserialize<IEnumerable<SingleStepTest>>(jsonString)!;
    } */

    private static Processor.Cpu CreateCpuAndRun(out Memory memory, SingleStepTest test)
    {
        memory =  new Memory();
        PopulateCpuMemory(test.Initial.Ram, memory);
        
        var initialRegisters = new Registers(
            test.Initial.Pc,
            test.Initial.S,
            test.Initial.A,
            test.Initial.X,
            test.Initial.Y,
            test.Initial.P);

        var bus = new Bus(memory);
        var cpu = new Processor.Cpu(initialRegisters, bus);

        cpu.RunInstruction();
        bus.Dispose();
        
        return cpu;
    }

    private static void AssertRegisters(Processor.Cpu cpu, Registers finalRegisters)
    {
        Assert.Multiple(() =>
        {
            Assert.That(cpu.Registers.Pc, Is.EqualTo(finalRegisters.Pc),
                $"PC mismatch: expected 0x{finalRegisters.Pc:X4} but was 0x{cpu.Registers.Pc:X4}");

            Assert.That(cpu.Registers.Sp, Is.EqualTo(finalRegisters.Sp),
                $"S mismatch: expected 0x{finalRegisters.Sp:X2} but was 0x{cpu.Registers.Sp:X2}");

            Assert.That(cpu.Registers.A, Is.EqualTo(finalRegisters.A),
                $"A mismatch: expected 0x{finalRegisters.A:X2} but was 0x{cpu.Registers.A:X2}");

            Assert.That(cpu.Registers.X, Is.EqualTo(finalRegisters.X),
                $"X mismatch: expected 0x{finalRegisters.X:X2} but was 0x{cpu.Registers.X:X2}");

            Assert.That(cpu.Registers.Y, Is.EqualTo(finalRegisters.Y),
                $"Y mismatch: expected 0x{finalRegisters.Y:X2} but was 0x{cpu.Registers.Y:X2}");

            Assert.That(cpu.Registers.P, Is.EqualTo(finalRegisters.P),
                $"P mismatch:\n" +
                $"Expected: {FormatPRegisterMessage(finalRegisters.P)}\n" +
                $"Actual:   {FormatPRegisterMessage(cpu.Registers.P)}"
            );
        });
    }

    private static void AssertMemory(Memory memory, ushort[][] expected)
    {
        foreach (var entry in expected)
        {
            var address = entry[0];
            var expectedValue = (byte)entry[1];
            var actualValue = memory[address];

            if (actualValue != expectedValue)
            {
                Assert.Fail(
                    $"Memory mismatch at 0x{address:X4}\n" +
                    $"Expected: 0x{expectedValue:X2}\n" +
                    $"Actual:   0x{actualValue:X2}");
            }
        }
    }

    private static void PopulateCpuMemory(ushort[][] initialMemory, Memory memory)
    {
        foreach (var row in initialMemory)
        {
            memory[row[0]] = (byte)row[1];
        }
    }

    private static string FormatPRegisterMessage(byte p)
    {
        return $"{Convert.ToString(p, 2).PadLeft(8, '0')} " +
               $"[N={(p >> 7) & 1} V={(p >> 6) & 1} B={(p >> 4) & 1} D={(p >> 3) & 1} I={(p >> 2) & 1} Z={(p >> 1) & 1} C={p & 1}]";
    }
    
    
    /*
private static IEnumerable<SingleStepTest> AllTestCases()
{
    foreach (var file in GetAllJsonFiles())
    {
        using var stream = File.OpenRead(file);

        var tests = JsonSerializer.Deserialize<IEnumerable<SingleStepTest>>(stream)!;

        foreach (var test in tests)
        {
            yield return test;
        }
    }
} */
}