use criterion::{criterion_group, criterion_main, Criterion};
use mpdfin::mpd::Request;

fn parse_requests(c: &mut Criterion) {
    c.bench_function("parse_basic_find", |b| {
        b.iter(|| Request::parse("find Artist Tool Album Lateralus").unwrap())
    });
}

criterion_group!(benches, parse_requests);
criterion_main!(benches);
