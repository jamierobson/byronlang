namespace Byron.Compiler.Tests.IntegrationTests;

public class StackallocStructProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() => """
                                           struct MyType {
                                               a: i32,
                                               b: i32,
                                               c: Point2d,
                                           }

                                           struct Point2d {
                                               x: i32,
                                               y: i32
                                           }

                                           fn main(): i32 {
                                               var myPoint: Point2d = Point2d { 
                                                   x: 100, 
                                                   y: 200,
                                               };
                                               
                                               myPoint = Point2d { 
                                                   x: 3, 
                                                   y: 4
                                               };
                                               
                                               var instance: MyType = getPoint(myPoint);
                                               instance.c.y = instance.c.y + 1;
                                               
                                               return instance.a + instance.b + instance.c.x + instance.c.y;
                                           }

                                           fn getPoint(point2d: Point2d): MyType {
                                               return MyType {
                                                   a: 1,
                                                   b: 2,
                                                   c: point2d
                                               };
                                           }
                                           """;
}