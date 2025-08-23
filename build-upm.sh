#!/bin/bash

# Unity Quest Vision Stream - UPM Build Script
# This script builds the Android plugin and prepares the UPM package

set -e  # Exit on any error

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR"
ANDROID_DIR="$PROJECT_ROOT/android"
UPM_DIR="$PROJECT_ROOT/com.questvisionstream"
PACKAGE_JSON="$UPM_DIR/package.json"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to display usage
show_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Build Unity Quest Vision Stream UPM package

OPTIONS:
    -a, --android-only     Build only Android plugin
    -c, --core-only        Update only core assets
    -s, --samples-only     Update only samples
    -h, --help            Show this help message

Default behavior (no options): Build everything (Android + Core + Samples)

Examples:
    $0                     # Build everything
    $0 --android-only      # Build only Android plugin
    $0 --core-only         # Update only core assets
    $0 --samples-only      # Update only samples
EOF
}

# Parse command line arguments
ANDROID_ONLY=false
CORE_ONLY=false
SAMPLES_ONLY=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -a|--android-only)
            ANDROID_ONLY=true
            shift
            ;;
        -c|--core-only)
            CORE_ONLY=true
            shift
            ;;
        -s|--samples-only)
            SAMPLES_ONLY=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Determine what to build
BUILD_ANDROID=true
BUILD_CORE=true
BUILD_SAMPLES=true

if [[ "$ANDROID_ONLY" == true ]]; then
    BUILD_CORE=false
    BUILD_SAMPLES=false
elif [[ "$CORE_ONLY" == true ]]; then
    BUILD_ANDROID=false
    BUILD_SAMPLES=false
elif [[ "$SAMPLES_ONLY" == true ]]; then
    BUILD_ANDROID=false
    BUILD_CORE=false
fi

log_info "Unity Quest Vision Stream UPM Build Script"
log_info "============================================="
log_info "Build Android: $BUILD_ANDROID"
log_info "Build Core: $BUILD_CORE"
log_info "Build Samples: $BUILD_SAMPLES"
echo

# Function to check Java version
check_java_version() {
    log_info "Checking Java version..."
    
    if ! command -v java &> /dev/null; then
        log_error "Java is not installed. Please install Java 17."
        exit 1
    fi
    
    JAVA_VERSION=$(java -version 2>&1 | head -n 1 | cut -d'"' -f2 | cut -d'.' -f1)
    
    if [[ "$JAVA_VERSION" != "17" ]]; then
        log_error "Java version $JAVA_VERSION detected. Please install Java 17."
        log_error "You can install Java 17 using:"
        log_error "  - macOS: brew install openjdk@17"
        log_error "  - Ubuntu: sudo apt install openjdk-17-jdk"
        log_error "  - Or download from: https://adoptium.net/"
        exit 1
    fi
    
    log_success "Java 17 detected"
}

# Function to check Android folder
check_android_folder() {
    log_info "Checking Android folder..."
    
    if [[ ! -d "$ANDROID_DIR" ]]; then
        log_error "Android folder does not exist at: $ANDROID_DIR"
        log_error "Please create the Android folder by:"
        log_error "1. Open Unity > File > Build Profiles"
        log_error "2. Enable 'Export Project' and 'Symlink Resources'"
        log_error "3. Create an android/ folder and pick it"
        exit 1
    fi
    
    # Check for required files
    REQUIRED_FILES=("build.gradle" "settings.gradle" "gradlew")
    for file in "${REQUIRED_FILES[@]}"; do
        if [[ ! -f "$ANDROID_DIR/$file" ]]; then
            log_error "Required file missing: $ANDROID_DIR/$file"
            log_error "Please ensure you have exported a complete Android project from Unity."
            exit 1
        fi
    done
    
    # Check for QuestVisionStreamPlugin.androidlib
    if [[ ! -d "$PROJECT_ROOT/Assets/Plugins/Android/QuestVisionStreamPlugin.androidlib" ]]; then
        log_error "QuestVisionStreamPlugin.androidlib not found in Assets/Plugins/Android/"
        log_error "Please ensure the Android plugin module exists."
        exit 1
    fi
    
    log_success "Android folder structure validated"
}

# Function to get current version from package.json
get_current_version() {
    if [[ ! -f "$PACKAGE_JSON" ]]; then
        log_error "package.json not found at: $PACKAGE_JSON"
        exit 1
    fi
    
    VERSION=$(grep '"version"' "$PACKAGE_JSON" | sed 's/.*"version": "\([^"]*\)".*/\1/')
    echo "$VERSION"
}

# Function to increment version based on semantic versioning
increment_version() {
    local current_version="$1"
    local increment_type="$2"
    
    IFS='.' read -ra VERSION_PARTS <<< "$current_version"
    local major="${VERSION_PARTS[0]}"
    local minor="${VERSION_PARTS[1]}"
    local patch="${VERSION_PARTS[2]}"
    
    case "$increment_type" in
        "major")
            major=$((major + 1))
            minor=0
            patch=0
            ;;
        "minor")
            minor=$((minor + 1))
            patch=0
            ;;
        "patch")
            patch=$((patch + 1))
            ;;
        *)
            log_error "Invalid increment type: $increment_type"
            exit 1
            ;;
    esac
    
    echo "$major.$minor.$patch"
}

# Function to update package.json version
update_package_version() {
    local new_version="$1"
    
    log_info "Updating package.json version to: $new_version"
    
    # Create backup
    cp "$PACKAGE_JSON" "$PACKAGE_JSON.backup"
    
    # Update version using sed
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' "s/\"version\": \"[^\"]*\"/\"version\": \"$new_version\"/" "$PACKAGE_JSON"
    else
        # Linux
        sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$new_version\"/" "$PACKAGE_JSON"
    fi
    
    log_success "Version updated to: $new_version"
}

# Function to build Android plugin
build_android() {
    log_info "Building Android plugin..."
    
    cd "$ANDROID_DIR"
    
    # Setup Gradle wrapper
    log_info "Setting up Gradle wrapper..."
    ./gradlew wrapper --gradle-version 8.9
    
    # Build the Android library
    log_info "Building QuestVisionStreamPlugin.androidlib..."
    ./gradlew :QuestVisionStreamPlugin.androidlib:assembleRelease --info
    
    # Check if build was successful
    AAR_SOURCE="$PROJECT_ROOT/Assets/Plugins/Android/QuestVisionStreamPlugin.androidlib/build/outputs/aar/QuestVisionStreamPlugin.androidlib-release.aar"
    if [[ ! -f "$AAR_SOURCE" ]]; then
        log_error "Build failed! AAR file not found at: $AAR_SOURCE"
        exit 1
    fi
    
    # Copy AAR to UPM package
    AAR_DESTINATION="$UPM_DIR/Runtime/Plugins/Android/"
    mkdir -p "$AAR_DESTINATION"
    
    log_info "Copying AAR to UPM package..."
    cp "$AAR_SOURCE" "$AAR_DESTINATION/QuestVisionStreamPlugin-release.aar"
    
    # Create/update meta file
    cat > "$AAR_DESTINATION/QuestVisionStreamPlugin-release.aar.meta" << EOF
fileFormatVersion: 2
guid: $(openssl rand -hex 16)
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Android: Android
    second:
      enabled: 1
      settings: {}
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
    
    cd "$PROJECT_ROOT"
    log_success "Android plugin built and copied successfully!"
}

# Function to copy directory contents excluding .meta files
copy_without_meta() {
    local source="$1"
    local destination="$2"
    
    if [[ ! -d "$source" ]]; then
        log_warning "Source directory does not exist: $source"
        return
    fi
    
    log_info "Copying $source to $destination (excluding .meta files)..."
    
    # Create destination directory
    mkdir -p "$destination"
    
    # Use rsync to copy excluding .meta files
    rsync -av --exclude="*.meta" "$source/" "$destination/"
    
    log_success "Copy completed: $source -> $destination"
}

# Function to update core assets
update_core_assets() {
    log_info "Updating core assets..."
    
    SOURCE_DIR="$PROJECT_ROOT/Assets/QuestVisionStream"
    DEST_DIR="$UPM_DIR/Samples~/QuestVisionStream"
    
    if [[ ! -d "$SOURCE_DIR" ]]; then
        log_error "Source directory not found: $SOURCE_DIR"
        exit 1
    fi
    
    # Remove existing core assets in destination
    if [[ -d "$DEST_DIR" ]]; then
        log_info "Removing existing core assets..."
        rm -rf "$DEST_DIR"
    fi
    
    # Copy core assets
    copy_without_meta "$SOURCE_DIR" "$DEST_DIR"
    
    log_success "Core assets updated successfully!"
}

# Function to update samples
update_samples() {
    log_info "Updating samples..."
    
    SOURCE_DIR="$PROJECT_ROOT/Assets/Samples"
    DEST_BASE_DIR="$UPM_DIR/Samples~"
    
    if [[ ! -d "$SOURCE_DIR" ]]; then
        log_warning "Source samples directory not found: $SOURCE_DIR"
        return
    fi
    
    # Copy each sample directory
    for sample_dir in "$SOURCE_DIR"/*; do
        if [[ -d "$sample_dir" ]]; then
            sample_name=$(basename "$sample_dir")
            dest_sample_dir="$DEST_BASE_DIR/$sample_name"
            
            copy_without_meta "$sample_dir" "$dest_sample_dir"
        fi
    done
    
    log_success "Samples updated successfully!"
}

# Main execution
main() {
    log_info "Starting UPM build process..."
    
    # Determine version increment type based on what's being built
    VERSION_INCREMENT="patch"
    if [[ "$BUILD_ANDROID" == true ]]; then
        VERSION_INCREMENT="major"
    elif [[ "$BUILD_CORE" == true && "$BUILD_SAMPLES" == true ]]; then
        VERSION_INCREMENT="minor"
    elif [[ "$BUILD_CORE" == true ]]; then
        VERSION_INCREMENT="minor"
    fi
    
    # Get current version and calculate new version
    CURRENT_VERSION=$(get_current_version)
    NEW_VERSION=$(increment_version "$CURRENT_VERSION" "$VERSION_INCREMENT")
    
    log_info "Version: $CURRENT_VERSION -> $NEW_VERSION (increment: $VERSION_INCREMENT)"
    
    # Execute build steps based on configuration
    if [[ "$BUILD_ANDROID" == true ]]; then
        check_java_version
        check_android_folder
        build_android
    fi
    
    if [[ "$BUILD_CORE" == true ]]; then
        update_core_assets
    fi
    
    if [[ "$BUILD_SAMPLES" == true ]]; then
        update_samples
    fi
    
    # Update package version
    update_package_version "$NEW_VERSION"
    
    log_success "UPM build completed successfully!"
    log_success "Package version: $NEW_VERSION"
    log_info "Package location: $UPM_DIR"
}

# Run main function
main "$@"
