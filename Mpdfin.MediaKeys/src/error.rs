use interoptopus::{ffi_type, patterns::result::FFIError};

#[ffi_type(patterns(ffi_error))]
#[repr(C)]
#[derive(Debug)]
pub enum MediaKeysError {
    Ok = 0,
    NullPassed = 1,
    Panic = 2,
    OtherError = 3,
}

// Gives special meaning to some of your error variants.
impl FFIError for MediaKeysError {
    const SUCCESS: Self = Self::Ok;
    const NULL: Self = Self::NullPassed;
    const PANIC: Self = Self::Panic;
}

impl From<souvlaki::Error> for MediaKeysError {
    fn from(_: souvlaki::Error) -> Self {
        Self::OtherError
    }
}
