#!/bin/bash
# Integration test runner for ForthVM
# Runs all example programs and reports results

set -e

SCRIPT_DIR="$(dirname "$0")"
cd "$SCRIPT_DIR"

echo "========================================="
echo "ForthVM Integration Test Suite"
echo "========================================="
echo ""

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
TOTAL=0
PASSED=0
FAILED=0

# Function to run a test
run_test() {
    local file=$1
    local description=$2

    TOTAL=$((TOTAL + 1))
    echo -n "Test $TOTAL: $description... "

    if ./fvm run "$file" > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
        PASSED=$((PASSED + 1))
    else
        echo -e "${RED}FAIL${NC}"
        FAILED=$((FAILED + 1))
    fi
}

# Function to run a test with output
run_test_verbose() {
    local file=$1
    local description=$2

    TOTAL=$((TOTAL + 1))
    echo ""
    echo "────────────────────────────────────────"
    echo "Test $TOTAL: $description"
    echo "────────────────────────────────────────"

    if ./fvm -s run "$file" 2>&1; then
        echo -e "${GREEN}✓ PASS${NC}"
        PASSED=$((PASSED + 1))
    else
        echo -e "${RED}✗ FAIL${NC}"
        FAILED=$((FAILED + 1))
    fi
}

echo "Running basic tests..."
echo ""

# Basic arithmetic
run_test_verbose "examples/test_add.hvm" "Simple addition (+ 2 3)"
run_test_verbose "examples/arithmetic_ops.hvm" "Nested arithmetic ((10+20)*2)"

# Lambda calculus
run_test_verbose "examples/identity.hvm" "Identity function"
run_test_verbose "examples/combinators.hvm" "SKI combinators"
run_test_verbose "examples/church_numerals.hvm" "Church numerals"

# Advanced features
run_test_verbose "examples/superposition.hvm" "Superposition &{a,b}"
run_test_verbose "examples/constructors.hvm" "Constructors #Tag{}"
run_test_verbose "examples/normalize_test.hvm" "Normalization test"

# Multi-function programs
run_test_verbose "examples/arithmetic.hvm" "Multi-function program"
run_test_verbose "examples/factorial.hvm" "Factorial computation"

# Performance benchmark
run_test_verbose "examples/benchmark.hvm" "Performance benchmark"

echo ""
echo "========================================="
echo "Test Summary"
echo "========================================="
echo "Total:  $TOTAL"
echo -e "Passed: ${GREEN}$PASSED${NC}"
echo -e "Failed: ${RED}$FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC} 🎉"
    exit 0
else
    echo -e "${RED}Some tests failed.${NC}"
    exit 1
fi
