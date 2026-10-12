#!/usr/bin/env bash
# Two network namespaces joined by a veth pair, so two messenger instances can run on one machine and still
# see each other through real UDP broadcast/multicast and TCP.   sudo ./lab.sh up | down
#   lab_a 10.9.0.1/24   (the original client)      lab_b 10.9.0.2/24   (the client under test)
set -euo pipefail
case "${1:-}" in
  up)
    ip netns add lab_a; ip netns add lab_b
    ip link add va type veth peer name vb
    ip link set va netns lab_a; ip link set vb netns lab_b
    ip netns exec lab_a bash -c 'ip addr add 10.9.0.1/24 dev va; ip link set va up; ip link set lo up; ip route add 224.0.0.0/4 dev va; ip route add 255.255.255.255 dev va'
    ip netns exec lab_b bash -c 'ip addr add 10.9.0.2/24 dev vb; ip link set vb up; ip link set lo up; ip route add 224.0.0.0/4 dev vb; ip route add 255.255.255.255 dev vb'
    echo "lab is up" ;;
  down)
    ip netns del lab_a 2>/dev/null || true; ip netns del lab_b 2>/dev/null || true; echo "lab is down" ;;
  *) echo "usage: sudo $0 up|down"; exit 1 ;;
esac
