namespace Byron.Compiler.Tests.IntegrationTests;

public class ModuleProgramWithReferenceToUnderlyingType : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        alias Trait = MyMod.MyTrait;
        alias Type = MyMod.Dupe;
        
        fn main():i32 {
            let b = Type {
                x: 1,
            };
            let fromB = Type.Trait.get(&b); // 2
            
            return fromB;
        }
        
        module MyMod {
            struct Dupe {
                x: i32,
            }
            
            trait MyTrait {
                fn get(self: &Self): i32
            }
            
            implement MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return Dupe.get(self);
                }
            }
            
            implement Dupe {
                fn get(self: &Self): i32 {
                    return 2 * self.*.x;
                }
            }
        }
        """;
}
public class ModuleProgramWithIncorrectModuleResolvedTests : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        alias DupeA = Module_A.Dupe;
        alias Module_A = My.ModuleA;
        alias Module_B = My.ModuleB;
        alias AliasedTrait = Module_B.MyTrait;
        
        fn main():i32 {
            let b = Module_B.Dupe {
                y: 1,
            };
            let fromTrait = Module_B.Dupe.Module_B.MyTrait.get(&b); // 1
            return fromTrait;
        }
        
        module My.ModuleA {
            struct Dupe {
                x: i32,
            }
            
            implement Dupe {
                fn get(self: &Self): i32 {
                    return 2 * self.*.x;
                }
            }
            
            implement My.ModuleB.MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return Dupe.get(self);
                }
            }
        }
        
        module My.ModuleB {
            trait MyTrait {
                fn get(self: &Self): i32
            }
            
            implement MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return self.*.y;
                }
            }
        
            struct Dupe {
                y: i32,
            }
        }
        """;
}

public class ModuleProgramTests : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        alias DupeA = Module_A.Dupe;
        alias Module_A = My.ModuleA;
        alias Module_B = My.ModuleB;
        alias AliasedTrait = Module_B.MyTrait;
        
        fn main():i32 {
            let a = DupeA {
                x: 5,
            };
            
            let b = Module_B.Dupe {
                y: 1,
            };
            
            let fromMethod = a.get(); // 10;
            let fromFullyQualifiedInterface = My.ModuleA.Dupe.My.ModuleB.MyTrait.get(&a); //10
            let fromInScope = DupeA.AliasedTrait.get(&a); //10
            let fromB = Module_B.Dupe.Module_B.MyTrait.get(&b); // 1
            
            return fromMethod + fromFullyQualifiedInterface + fromInScope + fromB; //31
        }
        
        module My.ModuleA {
            struct Dupe {
                x: i32,
            }
            
            implement Dupe {
                fn get(self: &Self): i32 {
                    return 2 * self.*.x;
                }
            }
            
            implement My.ModuleB.MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return Dupe.get(self);
                }
            }
        }
        
        module My.ModuleB {
            trait MyTrait {
                fn get(self: &Self): i32
            }
            
            implement MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return self.*.y;
                }
            }
        
            struct Dupe {
                y: i32,
            }
        }
        """;
}

public class ModuleProgramWithCrossModuleTrait : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        alias Type = My.ModuleA.Dupe;
        alias Trait = My.ModuleB.MyTrait;
        
        fn main():i32 {
            let a = Type { x: 5 };
            let result = Type.Trait.get(&a);  // expect 10
            return result;
        }
        
        module My.ModuleA {
            struct Dupe {
                x: i32,
            }
        
            implement Dupe {
                fn get(self: &Self): i32 {
                    return 2 * self.*.x;
                }
            }
        
            implement My.ModuleB.MyTrait for Dupe {
                fn get(self: &Self): i32 {
                    return Dupe.get(self);
                }
            }
        }
        
        module My.ModuleB {
            trait MyTrait {
                fn get(self: &Self): i32
            }
        }
        """;
}