#region license
// boo - an extensible programming language for the CLI
// Copyright (C) 2004 Rodrigo B. de Oliveira
//
// Permission is hereby granted, free of charge, to any person 
// obtaining a copy of this software and associated documentation 
// files (the "Software"), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, 
// publish, distribute, sublicense, and/or sell copies of the Software, 
// and to permit persons to whom the Software is furnished to do so, 
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY 
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE 
// OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
// Contact Information
//
// mailto:rbo@acm.org
#endregion

using System;
using System.Collections;
using Boo.Lang.Compiler.Ast;
using Boo.Lang.Compiler;

namespace Boo.Lang.Compiler.Steps
{
	public class NormalizeStatementModifiers : AbstractTransformerCompilerStep
	{
		public const string MainModuleMethodName = "__Main__";
		
		override public void Run()
		{
			Accept(CompileUnit.Modules);
		}
		
		override public void OnModule(Module node)
		{
			ClassDefinition moduleClass = new ClassDefinition();
			
			int removed = 0;			
			TypeMember[] members = node.Members.ToArray();
			for (int i=0; i<members.Length; ++i)
			{
				TypeMember member = members[i];
				if (member.NodeType == NodeType.Method)
				{
					member.Modifiers |= TypeMemberModifiers.Static;
					node.Members.RemoveAt(i-removed);
					moduleClass.Members.Add(member);
					++removed;
				}				
			}		
			
			if (node.Globals.Statements.Count > 0)
			{
				Method method = new Method(node.Globals.LexicalInfo);
				method.Parameters.Add(new ParameterDeclaration("argv", new ArrayTypeReference(new SimpleTypeReference("string"))));
				method.ReturnType = CreateBoundTypeReference(BindingManager.VoidTypeBinding);
				method.Body = node.Globals;
				method.Name = MainModuleMethodName;
				method.Modifiers = TypeMemberModifiers.Static | TypeMemberModifiers.Private;				
				moduleClass.Members.Add(method);
				
				node.Globals = null;
				AstAnnotations.SetEntryPoint(CompileUnit, method);
			}
			
			if (moduleClass.Members.Count > 0)
			{
				moduleClass.Members.Add(CreateConstructor(node, TypeMemberModifiers.Private));
			
				moduleClass.Name = BuildModuleClassName(node);
				moduleClass.Modifiers = TypeMemberModifiers.Public |
										TypeMemberModifiers.Final |
										TypeMemberModifiers.Transient;
				node.Members.Add(moduleClass);
				
				AstAnnotations.SetModuleClass(node, moduleClass);
			}
			
			Accept(node.Members);
		}
		
		void LeaveTypeDefinition(TypeDefinition node)
		{
			if (!node.IsVisibilitySet)
			{
				node.Modifiers |= TypeMemberModifiers.Public;
			}
		}
		
		override public void LeaveEnumDefinition(EnumDefinition node)
		{
			LeaveTypeDefinition(node);
		}
		
		override public void LeaveInterfaceDefinition(InterfaceDefinition node)
		{
			LeaveTypeDefinition(node);
		}
		
		override public void LeaveClassDefinition(ClassDefinition node)
		{
			LeaveTypeDefinition(node);
			
			if (!node.HasConstructor)
			{				
				node.Members.Add(CreateConstructor(node, TypeMemberModifiers.Public));
			}
		}
		
		override public void LeaveField(Field node)
		{
			if (!node.IsVisibilitySet)
			{
				node.Modifiers |= TypeMemberModifiers.Protected;
			}
		}
		
		override public void LeaveProperty(Property node)
		{
			if (!node.IsVisibilitySet)
			{
				node.Modifiers |= TypeMemberModifiers.Public;
			}
		}
		
		override public void LeaveMethod(Method node)
		{
			if (!node.IsVisibilitySet)
			{
				node.Modifiers |= TypeMemberModifiers.Public;
			}
		}
		
		override public void LeaveConstructor(Constructor node)
		{
			if (!node.IsVisibilitySet)
			{
				node.Modifiers |= TypeMemberModifiers.Public;
			}
		}		
		
		override public void LeaveExpressionStatement(ExpressionStatement node)
		{
			LeaveStatement(node);
		}
		
		override public void LeaveRaiseStatement(RaiseStatement node)
		{
			LeaveStatement(node);
		}
		
		override public void LeaveReturnStatement(ReturnStatement node)
		{
			LeaveStatement(node);
		}
		
		override public void LeaveBreakStatement(BreakStatement node)
		{
			LeaveStatement(node);
		}
		
		override public void LeaveContinueStatement(ContinueStatement node)
		{
			LeaveStatement(node);
		}
		
		public void LeaveStatement(Statement node)
		{
			if (null != node.Modifier)
			{
				switch (node.Modifier.Type)
				{
					case StatementModifierType.If:
					{	
						IfStatement stmt = new IfStatement(node.Modifier.LexicalInfo);
						stmt.Condition = node.Modifier.Condition;
						stmt.TrueBlock = new Block();						
						stmt.TrueBlock.Statements.Add(node);						
						node.Modifier = null;
						
						ReplaceCurrentNode(stmt);
						
						break;
					}
					
					case StatementModifierType.Unless:
					{
						UnlessStatement stmt = new UnlessStatement(node.Modifier.LexicalInfo);
						stmt.Condition = node.Modifier.Condition;
						stmt.Block.Statements.Add(node);
						node.Modifier = null;
						
						ReplaceCurrentNode(stmt);
						break;
					}
					
					case StatementModifierType.While:
					{
						WhileStatement stmt = new WhileStatement(node.Modifier.LexicalInfo);
						stmt.Condition = node.Modifier.Condition;
						stmt.Block.Statements.Add(node);
						node.Modifier = null;
						
						ReplaceCurrentNode(stmt);
						break;
					}
						
					default:
					{							
						Errors.Add(CompilerErrorFactory.NotImplemented(node, string.Format("modifier {0} supported", node.Modifier.Type)));
						break;
					}
				}
			}
		}
		
		override public void LeaveUnaryExpression(UnaryExpression node)
		{
			if (UnaryOperatorType.UnaryNegation == node.Operator)
			{
				if (NodeType.IntegerLiteralExpression == node.Operand.NodeType)
				{
					IntegerLiteralExpression integer = (IntegerLiteralExpression)node.Operand;
					integer.Value *= -1;
					integer.LexicalInfo = node.LexicalInfo;
					ReplaceCurrentNode(integer);
				}
			}
		}
		
		Constructor CreateConstructor(Node lexicalInfoProvider, TypeMemberModifiers modifiers)
		{
			Constructor constructor = new Constructor(lexicalInfoProvider.LexicalInfo);
			constructor.Modifiers = modifiers;
			return constructor;
		}
		
		string BuildModuleClassName(Module module)
		{
			string name = module.Name;
			if (null != name)
			{
				name = name.Substring(0, 1).ToUpper() + name.Substring(1) + "Module";
			}
			else
			{
				module.Name = name = string.Format("__Module{0}__", _context.AllocIndex());
			}
			return name;
		}
	}
}
