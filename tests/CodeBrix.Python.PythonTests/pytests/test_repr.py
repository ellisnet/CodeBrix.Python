# -*- coding: utf-8 -*-

"""Test __repr__ output"""

import System
import pytest
from CodeBrix.Python.TestSupport import ReprTest

def test_basic():
    """Test Point class which implements both ToString and __repr__ without inheritance"""
    ob = ReprTest.Point(1,2)
    # point implements ToString() and __repr__()
    assert ob.__repr__() == "Point(1,2)"
    assert str(ob) == "CodeBrix.Python.TestSupport.ReprTest+Point: X=1, Y=2"

def test_system_string():
    """Test system string"""
    ob = System.String("hello")
    assert str(ob) == "hello"
    assert "<System.String object at " in ob.__repr__()

def test_str_only():
    """Test class implementing ToString() but not __repr__()"""
    ob = ReprTest.Bar()
    assert str(ob) == "I implement ToString() but not __repr__()!"
    assert "<CodeBrix.Python.TestSupport.Bar object at " in ob.__repr__()

def test_hierarchy1():
    """Test inheritance hierarchy with base & middle class implementing ToString"""
    ob1 = ReprTest.BazBase()
    assert str(ob1) == "Base class implementing ToString()!"
    assert "<CodeBrix.Python.TestSupport.BazBase object at " in ob1.__repr__()

    ob2 = ReprTest.BazMiddle()
    assert str(ob2) == "Middle class implementing ToString()!"
    assert "<CodeBrix.Python.TestSupport.BazMiddle object at " in ob2.__repr__()

    ob3 = ReprTest.Baz()
    assert str(ob3) == "Middle class implementing ToString()!"
    assert "<CodeBrix.Python.TestSupport.Baz object at " in ob3.__repr__()

def bad_tostring():
    """Test ToString that can't be used by str()"""
    ob = ReprTest.Quux()
    assert str(ob) == "CodeBrix.Python.TestSupport.ReprTest+Quux"
    assert "<CodeBrix.Python.TestSupport.Quux object at " in ob.__repr__()

def bad_repr():
    """Test incorrect implementation of repr"""
    ob1 = ReprTest.QuuzBase()
    assert str(ob1) == "CodeBrix.Python.TestSupport.ReprTest+QuuzBase"
    assert "<CodeBrix.Python.TestSupport.QuuzBase object at " in ob.__repr__()
    
    ob2 = ReprTest.Quuz()
    assert str(ob2) == "CodeBrix.Python.TestSupport.ReprTest+Quuz"
    assert "<CodeBrix.Python.TestSupport.Quuz object at " in ob.__repr__()

    ob3 = ReprTest.Corge()
    with pytest.raises(Exception):
        str(ob3)
    with pytest.raises(Exception):
        ob3.__repr__()

    ob4 = ReprTest.Grault()
    with pytest.raises(Exception):
        str(ob4)
    with pytest.raises(Exception):
        ob4.__repr__()
