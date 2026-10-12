import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material

ApplicationWindow {
    id: window
    visible: true
    width: 400
    height: 720
    title: qsTr("LAN Messenger X")

    Material.theme: Material.System
    Material.primary: Material.Teal
    Material.accent: Material.Teal

    StackView {
        id: stackView
        anchors.fill: parent
        initialItem: contactListPageComponent
    }

    Component {
        id: contactListPageComponent
        ContactListPage {
            onContactSelected: function(userId, name) {
                stackView.push(chatPageComponent, { "userId": userId, "peerName": name })
            }
        }
    }

    Component {
        id: chatPageComponent
        ChatPage {}
    }
}
