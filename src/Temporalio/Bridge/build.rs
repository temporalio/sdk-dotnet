extern crate cbindgen;

use std::env;

fn main() {
    let crate_dir = env::var("CARGO_MANIFEST_DIR").unwrap();

    let changed = cbindgen::Builder::new()
        .with_config(cbindgen::Config {
            cpp_compat: true,
            ..Default::default()
        })
        .with_crate(crate_dir)
        .with_pragma_once(true)
        .with_language(cbindgen::Language::C)
        .generate()
        .expect("Unable to generate bindings")
        .write_to_file("include/temporal-sdk-bridge.h");

    // If this changed and an env var disallows change, error
    if let Ok(env_val) = env::var("TEMPORAL_SDK_BRIDGE_DISABLE_HEADER_CHANGE") {
        if changed && env_val == "true" {
            println!("cargo:warning=bridge's header file changed unexpectedly from what's on disk");
            std::process::exit(1);
        }
    }
}
