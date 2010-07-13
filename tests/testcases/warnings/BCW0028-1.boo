"""
BCW0028-1.boo(18,9): BCW0028: WARNING: Implicit downcast from 'Test' to 'System.Collections.IEnumerable'.
"""

import System.Collections

macro enableBCW0028:
	Parameters.EnableWarning("BCW0028")
enableBCW0028


class Test:
	pass

def Foo(x as IEnumerable):
	pass

Foo(Test())

