# Detectors package with lazy loading
# Only imports the requested detector when needed

def get_detector(detector_name):
    """Get detector function by name with lazy loading"""
    if detector_name == 'yolo':
        from .yolo_detector import detect_objects
        return detect_objects
    elif detector_name == 'florence2':
        from .florence2_detector import detect_objects
        return detect_objects
    elif detector_name == 'owlv2':
        from .owlv2_detector import detect_objects
        return detect_objects
    elif detector_name == 'grounding_dino':
        from .grounding_dino_detector import detect_objects
        return detect_objects
    elif detector_name == 'body':
        from .body_tracker import track_body
        return track_body
    else:
        return None
