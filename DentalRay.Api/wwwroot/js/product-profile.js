// ============================================================
// DentalRay Product Profile
// ============================================================
//
// Version 1 is intentionally focused on clinic data entry:
// clinics, dentists, patients, studies and radiology images.
//
// Advanced modules remain in the codebase and database so later releases can
// enable them without redesigning the data model. They are not exposed in the
// Version 1 user interface until their complete workflows are clinic-tested.
//
// ============================================================

window.DentalRayProductProfile = Object.freeze({
    release: "1.0",
    features: Object.freeze({
        financials: false,
        resourceSharing: false,
        imageAttachments: false
    })
});
