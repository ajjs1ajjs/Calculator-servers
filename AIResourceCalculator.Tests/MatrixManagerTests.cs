using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class MatrixManagerTests
{
    // Стаб, що повертає "стару" матрицю без політики модулів (App Server не обов'язковий, LMS увімкнено).
    private sealed class StaleDataService : IDataService
    {
        public SizingMatrix LoadMatrix()
        {
            var m = new SizingMatrix();
            foreach (var mod in m.DocumentFlowModules)
            {
                mod.IsMandatory = false;                  // старі дані не знали про IsMandatory
                if (mod.Name is "LMS" or "HR Portal") mod.IsEnabled = true; // помилково увімкнені
            }
            return m;
        }
        public void SaveMatrix(SizingMatrix matrix) { }
        public void ClearMatrix() { }
    }

    [Fact]
    public void Load_RestoresModulePolicy_FromCode()
    {
        var manager = new MatrixManager(new StaleDataService(), new SizingMatrix());

        foreach (var name in new[] { "App Server", "ROBOT", "Web" })
        {
            var mod = manager.Matrix.DocumentFlowModules.First(m => m.Name == name);
            Assert.True(mod.IsMandatory, $"{name} має бути обов'язковим");
            Assert.True(mod.IsEnabled);
        }

        foreach (var name in new[] { "LMS", "HR Portal" })
        {
            var mod = manager.Matrix.DocumentFlowModules.First(m => m.Name == name);
            Assert.False(mod.IsMandatory);
            Assert.False(mod.IsEnabled);
        }
    }
}
