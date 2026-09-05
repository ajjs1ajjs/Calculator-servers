using ResourceCalculator.Data;

namespace ResourceCalculator.Interfaces;

public interface IDataService
{
    void SaveMatrix(SizingMatrix matrix);
    SizingMatrix LoadMatrix();
    void ClearMatrix();
}
