#!/usr/bin/env bash
# Script to build release binaries locally for testing
# This mimics what the GitHub Actions release workflow does

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Default values
VERSION="${1:-1.0.0-local}"
PROJECT_PATH="src/Otapewin/Otapewin.csproj"
OUTPUT_BASE="publish"

# Platforms to build
PLATFORMS=(
    "linux-x64"
    "linux-arm64"
    "osx-x64"
    "osx-arm64"
)

# Parse arguments
CLEAN=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --clean)
            CLEAN=true
            shift
            ;;
        --version)
            VERSION="$2"
            shift 2
            ;;
        *)
            VERSION="$1"
            shift
            ;;
    esac
done

echo -e "${CYAN}🚀 Otapewin Local Release Builder${NC}"
echo -e "${CYAN}=================================${NC}"
echo ""

# Clean if requested
if [ "$CLEAN" = true ] && [ -d "$OUTPUT_BASE" ]; then
    echo -e "${YELLOW}🧹 Cleaning output directory...${NC}"
    rm -rf "$OUTPUT_BASE"
fi

# Create output directory
mkdir -p "$OUTPUT_BASE"

echo -e "${GREEN}📦 Building version: $VERSION${NC}"
echo -e "${GREEN}📋 Platforms: ${PLATFORMS[*]}${NC}"
echo ""

SUCCESS=()
FAILED=()

for PLATFORM in "${PLATFORMS[@]}"; do
    echo -e "${CYAN}⚙️  Building for $PLATFORM...${NC}"
    
    OUTPUT_PATH="$OUTPUT_BASE/$PLATFORM"
    
    if dotnet publish "$PROJECT_PATH" \
        --configuration Release \
        --runtime "$PLATFORM" \
        --self-contained true \
        --output "$OUTPUT_PATH" \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=true \
        /p:EnableCompressionInSingleFile=true \
        /p:DebugType=none \
        /p:DebugSymbols=false \
        /p:Version="$VERSION" \
        --verbosity minimal > /dev/null 2>&1; then
        
        echo -e "   ${GREEN}✅ Build successful${NC}"
        
        # Create archive
        ARCHIVE_NAME="otapewin-$PLATFORM.tar.gz"
        ARCHIVE_PATH="$OUTPUT_BASE/$ARCHIVE_NAME"
        
        cd "$OUTPUT_PATH"
        tar -czf "../../$ARCHIVE_PATH" ./*
        cd - > /dev/null
        
        echo -e "   ${GREEN}📦 Created: $ARCHIVE_NAME${NC}"
        
        # Get file size
        SIZE=$(du -h "$ARCHIVE_PATH" | cut -f1)
        echo -e "   ${GRAY}📊 Size: $SIZE${NC}"
        
        SUCCESS+=("$PLATFORM")
    else
        echo -e "   ${RED}❌ Build failed${NC}"
        FAILED+=("$PLATFORM")
    fi
    
    echo ""
done

# Summary
echo -e "${CYAN}=================================${NC}"
echo -e "${CYAN}📊 Build Summary${NC}"
echo -e "${CYAN}=================================${NC}"

if [ ${#SUCCESS[@]} -gt 0 ]; then
    echo -e "${GREEN}✅ Successful: ${#SUCCESS[@]}${NC}"
    for platform in "${SUCCESS[@]}"; do
        echo -e "${GREEN}   - $platform${NC}"
    done
fi

if [ ${#FAILED[@]} -gt 0 ]; then
    echo -e "${RED}❌ Failed: ${#FAILED[@]}${NC}"
    for platform in "${FAILED[@]}"; do
        echo -e "${RED}   - $platform${NC}"
    done
fi

echo ""
echo -e "${CYAN}📁 Output directory: $OUTPUT_BASE${NC}"
echo ""

if [ ${#FAILED[@]} -eq 0 ]; then
    echo -e "${GREEN}🎉 All builds completed successfully!${NC}"
    exit 0
else
    echo -e "${YELLOW}⚠️  Some builds failed. Check the output above for details.${NC}"
    exit 1
fi
