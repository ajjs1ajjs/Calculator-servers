using AIResourceCalculator.Data;

namespace AIResourceCalculator.Interfaces;

public interface IDataService
{
    void SaveMatrix(SizingMatrix matrix);
    SizingMatrix LoadMatrix();
    void ClearMatrix();
}
