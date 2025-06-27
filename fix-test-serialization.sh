#!/bin/bash

echo "Fixing test serialization issues..."

# Fix pattern 1: var service = new Service(); var bridge = Bridge.Create(() => service);
# Replace with: var bridge = Bridge.Create(() => new Service());
find tests -name "*.cs" -type f -exec sed -i -E '
# Match lines with var service = new Something(); followed by Bridge.Create(() => service)
/var [a-zA-Z_][a-zA-Z0-9_]* = new [a-zA-Z_][a-zA-Z0-9_]*\([^)]*\);/ {
    # Store the line
    h
    # Read next lines until we find the Bridge.Create pattern
    :loop
    n
    /Bridge\.Create\(() => [a-zA-Z_][a-zA-Z0-9_]*\)/ {
        # Extract the service type from the stored line
        x
        s/.*var [a-zA-Z_][a-zA-Z0-9_]* = new ([a-zA-Z_][a-zA-Z0-9_]*)\(([^)]*)\);.*/\1(\2)/
        # Create the replacement line
        s/.*/            var bridge = XXXBridge.Create(() => new &);/
        # Now get the current line and extract the bridge type
        x
        s/.*([a-zA-Z_][a-zA-Z0-9_]*)Bridge\.Create.*/\1/
        # Combine them
        x
        s/XXX/&/
        p
        # Skip the next line (the old Bridge.Create line)
        n
        b
    }
    # If not found, put back and continue
    x
}
' {} + 2>/dev/null || true

echo "Manual fixes needed for complex cases..."