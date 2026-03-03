using System.Text.Json;
using Velutia.Cpu.Tests.Models;
using Velutia.Processor;

namespace Velutia.Cpu.Tests;

public class Tests
{
    [TestCaseSource(nameof(AllTestFiles))]
    public void CpuTest(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var tests = JsonSerializer.Deserialize<List<SingleStepTest>>(stream)!;

        foreach (var test in tests)
        {
            var cpu = CreateCpuAndRun(out var memory, test);

            AssertRegisters(cpu, new Registers(
                    test.Final.Pc,
                    test.Final.S,
                    test.Final.A,
                    test.Final.X,
                    test.Final.Y,
                    test.Final.P),
                test.Name);

            AssertMemory(memory, test.Final.Ram, test.Name);
            
            AssertInstructionCycles(cpu, test);
        }
    }

    private static IEnumerable<TestCaseData> AllTestFiles()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        var testPath = Path.Combine(testDir, "Data", "SingleStepTests");

        foreach (var testFile in Directory.EnumerateFiles(testPath, "*.json", SearchOption.AllDirectories))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(testFile);
            var testCase = new TestCaseData(testFile)
                .SetName($"Instruction: {fileNameWithoutExtension}");

            yield return testCase;
        }
    }

    private static IEnumerable<TestCaseData> IndividualTestFile()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        var testPath = Path.Combine(testDir, "Data", "SingleStepTests");
        var testFile = Path.Combine(testPath, "75.json");

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(testFile);
        var testCase = new TestCaseData(testFile)
            .SetName($"Instruction: {fileNameWithoutExtension}");

        yield return testCase;
    }

    private static Processor.Cpu CreateCpuAndRun(out Memory memory, SingleStepTest test)
    {
        memory = new Memory();
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

    private static void AssertRegisters(Processor.Cpu cpu, Registers finalRegisters, string testName)
    {
        Assert.Multiple(() =>
        {
            Assert.That(cpu.Registers.Pc, Is.EqualTo(finalRegisters.Pc),
                $"Test: {testName} PC mismatch: expected 0x{finalRegisters.Pc:X4} but was 0x{cpu.Registers.Pc:X4}");

            Assert.That(cpu.Registers.Sp, Is.EqualTo(finalRegisters.Sp),
                $"S mismatch: expected 0x{finalRegisters.Sp:X2} but was 0x{cpu.Registers.Sp:X2}");

            Assert.That(cpu.Registers.A, Is.EqualTo(finalRegisters.A),
                $"A mismatch: expected 0x{finalRegisters.A:X2} but was 0x{cpu.Registers.A:X2}");

            Assert.That(cpu.Registers.X, Is.EqualTo(finalRegisters.X),
                $"X mismatch: expected 0x{finalRegisters.X:X2} but was 0x{cpu.Registers.X:X2}");

            Assert.That(cpu.Registers.Y, Is.EqualTo(finalRegisters.Y),
                $"Y mismatch: expected 0x{finalRegisters.Y:X2} but was 0x{cpu.Registers.Y:X2}");

            Assert.That(cpu.Registers.P, Is.EqualTo(finalRegisters.P),
                $"Test: {testName} P mismatch:\n" +
                $"Expected: {FormatPRegisterMessage(finalRegisters.P)}\n" +
                $"Actual:   {FormatPRegisterMessage(cpu.Registers.P)}"
            );
        });
    }

    private void AssertInstructionCycles(Processor.Cpu cpu, SingleStepTest test)
    {
        Assert.That(cpu.Cycles, Is.EqualTo(test.Cycles.Length),
            $"Cycles mismatch. Expected: {test.Cycles.Length} cycles\nActual: {cpu.Cycles} cycles");
    }

    private static void AssertMemory(Memory memory, ushort[][] expected, string testName)
    {
        foreach (var entry in expected)
        {
            var address = entry[0];
            var expectedValue = (byte)entry[1];
            var actualValue = memory[address];

            if (actualValue != expectedValue)
            {
                Assert.Fail(
                    $"Test: {testName}\n" +
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
               $"[N={(p >> 7) & 1} V={(p >> 6) & 1} -={(p >> 5) & 1} B={(p >> 4) & 1} D={(p >> 3) & 1} I={(p >> 2) & 1} Z={(p >> 1) & 1} C={p & 1}]";
    }
}