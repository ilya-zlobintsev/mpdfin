use interoptopus::util::NamespaceMappings;
use interoptopus::{Error, Interop};

#[test]
fn bindings_csharp() -> Result<(), Error> {
    use interoptopus_backend_csharp::overloads::DotNet;
    use interoptopus_backend_csharp::{Config, Generator};

    let config = Config {
        dll_name: "mpdfin_mediakeys".to_string(),
        namespace_mappings: NamespaceMappings::new("Mpdfin.Interop"),
        class: "MediaKeysController".to_owned(),
        ..Config::default()
    };

    Generator::new(config, mpdfin_mediakeys::my_inventory())
        .add_overload_writer(DotNet::new())
        .write_file("../Mpdfin/Interop/MediaKeysController.cs")?;

    Ok(())
}
