namespace Byron.Compiler.Tests.IntegrationTests;

public class SimpleTraitProgramTests : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        trait Point2d {
            x: i32,
            y: i32,
            fn getSum(point: &Self): i32,
        }
        
        struct Point {
          x: i32,
          y: i32,
          z: i32,
        }
        
        implement Point {
            fn getSum(self: &Self): i32 {
                return 1000;
            }
        }
        
        implement Point2d for Point {
            fn getSum(point: &Self): i32 {
                return point.*.x + point.*.y;
            }
        }
        
        fn main(): i32 {
            let point = Point {
                x: 1,
                y: 10,
                z: 100,
            };
            
            var count = point.getSum(); //1000
            count = count + Point.getSum(&point); //2000
            count = count + Point.Point2d.getSum(&point); // 2011
            
            return count; //expect 2011 or 219;
        }
        """;
}

public class TraitImplementationsWithSameTypesButDifferentLiteralSymbols : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        fn main(): i32 {
            return 0;
        }
        
        implement A.MyTrait for A.MyType {
            fn get(): A.MyType {
                return A.MyType {a: 3};
            }
        }
        
        module A {
            struct MyType {a: i32}
            trait MyTrait {
                fn get(): MyType,
            }
        }
        """;
}

public class ComplexTraitProgramTests : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        trait Point2d {
            x: i32,
            y: i32,
            fn getY(self: &Self): i32,
            fn getSum(self: &Self): i32,
        }
        
        trait Point3d {
            x: i32,
            y: i32,
            z: i32,
            fn getZ(self: &Self): i32,
            fn getSum(self: &Self): i32,
        }
        
        struct Point {
          x: i32,
          y: i32,
          z: i32,
        }
        
        implement Point {
            fn getSum(self: &Self): i32 {
                return 1000;
            }
        }
        
        implement Point2d for Point {
            fn getY(self: &Self): i32 {
                return self.*.y;
            }
            
            fn getSum(self: &Self): i32 {
                return self.*.x + self.*.y;
            }
        }
        
        implement Point3d for Point {
            fn getZ(self: &Self): i32 {
                return self.*.z;
            }
            
            fn getSum(self: &Self): i32 {
                return self.*.x + self.*.y + self.*.z;
            }
        }
        
        
        fn main(): i32 {
            let point = Point {x: 1, y: 10, z: 100,};
            let fromImplementBlock = point.getSum(); // expect 1000
            let fromPoint2d = Point.Point2d.getSum(&point); // expect 11
            let fromPoint3d = Point.Point3d.getSum(&point); // expect 111
            
            var sumOfSums = fromImplementBlock + fromPoint2d + fromPoint3d;
            
            if(sumOfSums != 1122)
            {
                return 255; 
            }
            
            let fieldAccess = point.x + Point.Point2d.getY(&point) + Point.Point3d.getZ(&point); // expect  1 + 10 + 100 = 111 
            sumOfSums = sumOfSums - fieldAccess; // expect 1122 - 111 = 1011
            
            return sumOfSums;
        }
        """;
}