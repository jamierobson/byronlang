namespace Byron.Compiler.Tests.IntegrationTests;

public class TypeInferenceProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() => """
                                           struct PointInSpace {
                                               value: i32,
                                           }

                                           struct Point2d {
                                               x: PointInSpace,
                                               y: PointInSpace
                                           }

                                           fn main(): i32 {
                                               var myPoint = Point2d { 
                                                   x: PointInSpace { value: 10, }, 
                                                   y: PointInSpace { value: 20 },
                                               };
                                               
                                               myPoint.x = PointInSpace { value: 1 };
                                               
                                               myPoint.x.value = myPoint.y.value + myPoint.x.value + 2;
                                               
                                               let mySum = add(myPoint.x, myPoint.y);
                                               return mySum; 
                                           }

                                           fn add(a: PointInSpace, b: PointInSpace): i32 {
                                               let result = a.value + b.value;
                                               return result;
                                           }
                                           """;
}