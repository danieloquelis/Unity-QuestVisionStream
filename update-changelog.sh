#!/bin/bash

# Unity Quest Vision Stream - Changelog Update Script
# This script generates changelog entries based on git commits since the last tag

set -e  # Exit on any error

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR"
UPM_DIR="$PROJECT_ROOT/com.questvisionstream"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1" >&2
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1" >&2
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

# Function to display usage
show_usage() {
    cat << EOF
Usage: $0 [VERSION] [LATEST_TAG]

Generate changelog entry for Unity Quest Vision Stream

ARGUMENTS:
    VERSION      The new version number (e.g., 1.2.0)
    LATEST_TAG   The latest git tag to compare against (optional)

Examples:
    $0 1.2.0                    # Generate changelog for version 1.2.0 since last tag
    $0 1.2.0 v1.1.0            # Generate changelog for version 1.2.0 since v1.1.0
    $0 --help                  # Show this help message
EOF
}

# Function to get the latest git tag
get_latest_tag() {
    local latest_tag=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
    echo "$latest_tag"
}

# Function to parse conventional commit type
get_commit_type() {
    local commit_message="$1"
    
    if [[ "$commit_message" =~ ^feat(\(.*\))?!?: ]]; then
        echo "### Features"
    elif [[ "$commit_message" =~ ^fix(\(.*\))?!?: ]]; then
        echo "### Bug Fixes"
    elif [[ "$commit_message" =~ ^docs(\(.*\))?!?: ]]; then
        echo "### Documentation"
    elif [[ "$commit_message" =~ ^style(\(.*\))?!?: ]]; then
        echo "### Style"
    elif [[ "$commit_message" =~ ^refactor(\(.*\))?!?: ]]; then
        echo "### Code Refactoring"
    elif [[ "$commit_message" =~ ^perf(\(.*\))?!?: ]]; then
        echo "### Performance Improvements"
    elif [[ "$commit_message" =~ ^test(\(.*\))?!?: ]]; then
        echo "### Tests"
    elif [[ "$commit_message" =~ ^build(\(.*\))?!?: ]]; then
        echo "### Build System"
    elif [[ "$commit_message" =~ ^ci(\(.*\))?!?: ]]; then
        echo "### CI"
    elif [[ "$commit_message" =~ ^chore(\(.*\))?!?: ]]; then
        echo "### Chores"
    else
        echo "### Other Changes"
    fi
}

# Function to format commit message for changelog
format_commit_message() {
    local commit_hash="$1"
    local commit_message="$2"
    
    # Remove conventional commit prefix and clean up
    local clean_message=$(echo "$commit_message" | sed -E 's/^[a-z]+(\([^)]*\))?!?: //')
    # Capitalize first letter
    clean_message="$(echo "${clean_message:0:1}" | tr '[:lower:]' '[:upper:]')${clean_message:1}"
    
    # Add commit hash for reference
    echo "- $clean_message ([${commit_hash:0:7}](https://github.com/danieloquelis/Unity-QuestVisionStream/commit/$commit_hash))"
}

# Function to generate changelog entry
generate_changelog_entry() {
    local new_version="$1"
    local latest_tag="$2"
    
    log_info "Generating changelog entry for version $new_version..."
    
    # Get current date
    local current_date=$(date +"%Y-%m-%d")
    
    # Initialize changelog content
    local changelog_header="## $new_version - $current_date"
    local changelog_content=""
    
    # Get commits since last tag
    local range=""
    if [[ -n "$latest_tag" ]]; then
        range="$latest_tag..HEAD"
        log_info "Getting commits since tag: $latest_tag"
    else
        range="HEAD"
        log_info "No previous tag found, getting all commits"
    fi
    
    # Get commits with hash and message
    local commits=$(git log --pretty=format:"%H|%s" "$range" --reverse)
    
    if [[ -z "$commits" ]]; then
        log_warning "No commits found since last tag"
        changelog_content="### Other Changes\n- Version bump"
    else
        # Group commits by type using simpler approach (compatible with older bash)
        local features=""
        local bug_fixes=""
        local performance=""
        local refactoring=""
        local documentation=""
        local style=""
        local tests=""
        local build_system=""
        local ci=""
        local chores=""
        local other_changes=""
        
        # Process each commit
        while IFS='|' read -r commit_hash commit_message; do
            if [[ -n "$commit_hash" && -n "$commit_message" ]]; then
                local commit_type=$(get_commit_type "$commit_message")
                local formatted_message=$(format_commit_message "$commit_hash" "$commit_message")
                
                case "$commit_type" in
                    "### Features")
                        if [[ -z "$features" ]]; then
                            features="$formatted_message"
                        else
                            features+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Bug Fixes")
                        if [[ -z "$bug_fixes" ]]; then
                            bug_fixes="$formatted_message"
                        else
                            bug_fixes+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Performance Improvements")
                        if [[ -z "$performance" ]]; then
                            performance="$formatted_message"
                        else
                            performance+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Code Refactoring")
                        if [[ -z "$refactoring" ]]; then
                            refactoring="$formatted_message"
                        else
                            refactoring+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Documentation")
                        if [[ -z "$documentation" ]]; then
                            documentation="$formatted_message"
                        else
                            documentation+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Style")
                        if [[ -z "$style" ]]; then
                            style="$formatted_message"
                        else
                            style+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Tests")
                        if [[ -z "$tests" ]]; then
                            tests="$formatted_message"
                        else
                            tests+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Build System")
                        if [[ -z "$build_system" ]]; then
                            build_system="$formatted_message"
                        else
                            build_system+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### CI")
                        if [[ -z "$ci" ]]; then
                            ci="$formatted_message"
                        else
                            ci+=$'\n'"$formatted_message"
                        fi
                        ;;
                    "### Chores")
                        if [[ -z "$chores" ]]; then
                            chores="$formatted_message"
                        else
                            chores+=$'\n'"$formatted_message"
                        fi
                        ;;
                    *)
                        if [[ -z "$other_changes" ]]; then
                            other_changes="$formatted_message"
                        else
                            other_changes+=$'\n'"$formatted_message"
                        fi
                        ;;
                esac
            fi
        done <<< "$commits"
        
        # Build changelog content in order
        if [[ -n "$features" ]]; then
            changelog_content+="### Features"$'\n'"$features"$'\n\n'
        fi
        if [[ -n "$bug_fixes" ]]; then
            changelog_content+="### Bug Fixes"$'\n'"$bug_fixes"$'\n\n'
        fi
        if [[ -n "$performance" ]]; then
            changelog_content+="### Performance Improvements"$'\n'"$performance"$'\n\n'
        fi
        if [[ -n "$refactoring" ]]; then
            changelog_content+="### Code Refactoring"$'\n'"$refactoring"$'\n\n'
        fi
        if [[ -n "$documentation" ]]; then
            changelog_content+="### Documentation"$'\n'"$documentation"$'\n\n'
        fi
        if [[ -n "$style" ]]; then
            changelog_content+="### Style"$'\n'"$style"$'\n\n'
        fi
        if [[ -n "$tests" ]]; then
            changelog_content+="### Tests"$'\n'"$tests"$'\n\n'
        fi
        if [[ -n "$build_system" ]]; then
            changelog_content+="### Build System"$'\n'"$build_system"$'\n\n'
        fi
        if [[ -n "$ci" ]]; then
            changelog_content+="### CI"$'\n'"$ci"$'\n\n'
        fi
        if [[ -n "$chores" ]]; then
            changelog_content+="### Chores"$'\n'"$chores"$'\n\n'
        fi
        if [[ -n "$other_changes" ]]; then
            changelog_content+="### Other Changes"$'\n'"$other_changes"$'\n\n'
        fi
    fi
    
    # Create the full changelog entry
    local full_entry="$changelog_header"$'\n\n'"$changelog_content"
    
    echo "$full_entry"
}

# Function to update changelog
update_changelog() {
    local new_version="$1"
    local latest_tag="$2"
    
    local changelog_file="$UPM_DIR/CHANGELOG.md"
    
    log_info "Updating CHANGELOG.md..."
    
    # Generate new changelog entry
    local new_entry=$(generate_changelog_entry "$new_version" "$latest_tag")
    
    
    # Read existing changelog
    local existing_content=""
    if [[ -f "$changelog_file" ]]; then
        # Skip the first line (# Changelog) and read the rest
        existing_content=$(tail -n +2 "$changelog_file")
    fi
    
    # Write new changelog
    {
        echo "# Changelog"
        echo ""
        echo "$new_entry"
        if [[ -n "$existing_content" ]]; then
            echo "$existing_content"
        fi
    } > "$changelog_file"
    
    log_success "CHANGELOG.md updated successfully!"
}

# Main execution
main() {
    # Parse command line arguments
    case "${1:-}" in
        -h|--help)
            show_usage
            exit 0
            ;;
        "")
            log_error "Version number is required"
            show_usage
            exit 1
            ;;
    esac
    
    local new_version="$1"
    local latest_tag="${2:-}"
    
    # If no tag provided, try to get the latest one
    if [[ -z "$latest_tag" ]]; then
        latest_tag=$(get_latest_tag)
    fi
    
    log_info "Unity Quest Vision Stream - Changelog Update"
    log_info "=============================================="
    log_info "New version: $new_version"
    if [[ -n "$latest_tag" ]]; then
        log_info "Since tag: $latest_tag"
    else
        log_info "No previous tag found"
    fi
    echo
    
    # Check if we're in a git repository
    if ! git rev-parse --git-dir >/dev/null 2>&1; then
        log_error "Not in a git repository"
        exit 1
    fi
    
    # Check if UPM directory exists
    if [[ ! -d "$UPM_DIR" ]]; then
        log_error "UPM directory not found: $UPM_DIR"
        exit 1
    fi
    
    # Update changelog
    update_changelog "$new_version" "$latest_tag"
    
    log_success "Changelog update completed!"
    log_info "File location: $UPM_DIR/CHANGELOG.md"
}

# Run main function
main "$@"
